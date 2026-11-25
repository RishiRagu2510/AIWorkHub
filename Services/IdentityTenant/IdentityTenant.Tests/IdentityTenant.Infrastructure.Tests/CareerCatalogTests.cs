using IdentityTenant.Domain.Careers;
using IdentityTenant.Domain.Employees;
using IdentityTenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace IdentityTenant.Infrastructure.Tests;

public sealed class CareerDatabase : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17").Build();
    public IdentityTenantDbContext Open() => new(new DbContextOptionsBuilder<IdentityTenantDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);
    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        await using var db = Open();
        await db.Database.MigrateAsync();

        // Migration 024 intentionally clears the catalogue for an organization with
        // no employees. Verify that contract before installing this suite's test data.
        Assert.Empty(await db.CareerProfiles.ToListAsync());
        Assert.Empty(await db.Grades.ToListAsync());
        Assert.Empty(await db.CareerLevels.ToListAsync());
        foreach (var name in new[] { "011_career_catalog.sql", "014_software_engineer_titles.sql", "019_employee_skill_master.sql", "020_career_catalog_completion.sql" })
        {
            using var stream = typeof(IdentityTenantDbContext).Assembly.GetManifestResourceStream("IdentityTenant.Sql." + name)!;
            using var reader = new StreamReader(stream);
            await db.Database.ExecuteSqlRawAsync(await reader.ReadToEndAsync());
        }
    }
    public Task DisposeAsync() => postgres.DisposeAsync().AsTask();
}
public sealed class CareerCatalogTests(CareerDatabase fixture) : IClassFixture<CareerDatabase>
{
    private static readonly string[] Profiles = [
        "AI SDLC Engineer","Analyst","Architect","Backend Developer Profiles","Big Data Engineer Profiles",
        "Customer Support Analyst","Data Scientist","Delivery Management","Delivery Manager","Design Profiles",
        "DevOps Engineer","HR Profiles","Hybris Developer","Machine Learning Engineer","Mobile Developer Profiles",
        "NOC Engineer","Nontechnical Requirements for Engineers","Quality Engineer Profiles","Recruitment",
        "UI App Engineer","UI Developer Profiles","UI Mobile Engineer",".NET Developer"];

    [Fact] public async Task ExplicitCatalogSeedContainsAuthoritativeProfilesAndGradesWithoutSkills()
    {
        await using var db=fixture.Open();
        Assert.All(Profiles, name => Assert.Contains(name, db.CareerProfiles.Select(p=>p.Name)));
        Assert.Equal(31, await db.CareerProfiles.Select(p=>p.Code).Distinct().CountAsync());
        Assert.Equal(Enumerable.Range(0,7).Select(i=>$"T{i}"), await db.Grades.Where(g=>g.Family=="Engineering").OrderBy(g=>g.DisplayOrder).Select(g=>g.Code).ToListAsync());
        Assert.Equal(4,await db.Grades.CountAsync(g=>g.Family=="HR"));
        Assert.All(await db.CareerProfiles.ToListAsync(), p=>Assert.True(p.IsActive));
        Assert.Empty(await db.Skills.ToListAsync()); Assert.Empty(await db.CareerLevelSkills.ToListAsync());
        Assert.Equal(75, await db.CareerLevels.CountAsync());
    }
    [Theory]
    [InlineData("DOTNET_DEVELOPER", "T0:Intern|T1:Junior Software Engineer|T2:Software Engineer|T3:Senior Software Engineer|T4:Staff Software Engineer")]
    [InlineData("DATA_SCIENTIST", "T2:Junior Data Scientist|T3:Data Scientist|T4:Senior Data Scientist|T5:Staff Data Scientist|T6:Senior Staff Data Scientist")]
    [InlineData("AI_SDLC_ENGINEER", "T3:AI SDLC Engineer")]
    public async Task ConfirmedMappingsAreExact(string code,string expected)
    {
        await using var db=fixture.Open();
        var rows=await db.CareerLevels.Where(l=>l.CareerProfile.Code==code).OrderBy(l=>l.DisplayOrder).Select(l=>l.Grade.Code+":"+l.CareerTitle).ToListAsync();
        Assert.Equal(expected.Split('|'),rows);
        Assert.False(await db.CareerLevels.AnyAsync(l=>l.CareerTitle=="Principal Data Scientist"));
    }
    [Fact] public async Task EfCreationAndUniqueSkillMappingWork()
    {
        await using var db=fixture.Open(); await using var tx=await db.Database.BeginTransactionAsync();
        var profile=new CareerProfile{Code="TEST_ONLY",Name="Test catalogue"};
        var grade=new Grade{Code="TEST_ONLY",Name="Test grade",DisplayOrder=99};
        var skill=new Skill{Code="TEST_ONLY",Name="Test skill"};
        var level=new CareerLevel{CareerProfile=profile,Grade=grade,CareerTitle="Test title"};
        db.CareerLevelSkills.Add(new CareerLevelSkill{CareerLevel=level,Skill=skill,RequiredProficiency=1,TargetProficiency=2});
        await db.SaveChangesAsync();
        Assert.True(level.Id>0); Assert.True(profile.Id>0); Assert.True(grade.Id>0);
        db.CareerLevelSkills.Add(new CareerLevelSkill{CareerLevelId=level.Id,SkillId=skill.Id,RequiredProficiency=1,TargetProficiency=2});
        var error=await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation,Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }
    [Theory]
    [InlineData("INSERT INTO career_profiles(code,name) VALUES('DOTNET_DEVELOPER','Duplicate')", "23505")]
    [InlineData("INSERT INTO grades(code,name,display_order) VALUES('T0','Duplicate',0)", "23505")]
    [InlineData("INSERT INTO career_levels(career_profile_id,grade_id,career_title,display_order) SELECT career_profile_id,grade_id,'Duplicate',0 FROM career_levels LIMIT 1", "23505")]
    [InlineData("INSERT INTO career_levels(career_profile_id,grade_id,career_title,display_order) VALUES(-1,-1,'Missing',0)", "23503")]
    [InlineData("INSERT INTO career_level_skills(career_level_id,skill_id,required_proficiency,target_proficiency) VALUES(-1,-1,1,2)", "23503")]
    [InlineData("DELETE FROM career_profiles WHERE code='DOTNET_DEVELOPER'", "23503")]
    public async Task ConstraintsRejectInvalidMasterData(string sql,string state)
    {
        await using var db=fixture.Open(); await using var tx=await db.Database.BeginTransactionAsync();
        var ex=await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlRawAsync(sql)); Assert.Equal(state,ex.SqlState);
    }
    [Fact] public async Task EmployeeCareerLinksDeriveTitleAndRejectMismatchedProfile()
    {
        await using var db=fixture.Open(); await using var tx=await db.Database.BeginTransactionAsync();
        var tenant=await db.Database.SqlQueryRaw<long>("INSERT INTO tenants(name,slug) VALUES('Test tenant','career-test') RETURNING id AS \"Value\"").ToListAsync();
        var tenantId=tenant.Single();
        var user=await db.Database.SqlQuery<long>($"INSERT INTO users(tenant_id,email,display_name) VALUES({tenantId},'test@example.invalid','Test') RETURNING id AS \"Value\"").ToListAsync();
        var e=new Employee{TenantId=tenantId,UserId=user.Single(),EmployeeCode="TEST",FirstName="Test",LastName="Employee",WorkEmail="test@example.invalid",JoiningDate=new DateOnly(2025,1,1)};
        db.Add(e); await db.SaveChangesAsync();
        Assert.Null(e.CareerLevelId);
        var level=await db.CareerLevels.Include(l=>l.Grade).SingleAsync(l=>l.CareerProfile.Code=="DOTNET_DEVELOPER"&&l.Grade.Code=="T3");
        e.CareerProfileId=level.CareerProfileId;e.CareerLevelId=level.Id; await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var loaded=await db.Set<Employee>().Include(x=>x.CareerLevel!).ThenInclude(x=>x.Grade).SingleAsync(x=>x.Id==e.Id);
        Assert.Equal("Senior Software Engineer",loaded.CareerLevel!.CareerTitle);Assert.Equal("T3",loaded.CareerLevel.Grade.Code);
        var other=await db.CareerProfiles.SingleAsync(p=>p.Code=="DATA_SCIENTIST");
        var ex=await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE employees SET career_profile_id={other.Id} WHERE id={e.Id}"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation,ex.SqlState);
    }
    [Fact] public async Task MigrationAndSqlAreRepeatableAndSnapshotHasNoPendingChanges()
    {
        await using var db=fixture.Open();
        Assert.False(db.Database.HasPendingModelChanges());
        await db.Database.MigrateAsync();await db.Database.MigrateAsync();
        using var stream=typeof(IdentityTenantDbContext).Assembly.GetManifestResourceStream("IdentityTenant.Sql.019_employee_skill_master.sql")!;
        using var reader=new StreamReader(stream);var sql=await reader.ReadToEndAsync();
        await db.Database.ExecuteSqlRawAsync(sql);await db.Database.ExecuteSqlRawAsync(sql);
        Assert.Equal(31,await db.CareerProfiles.CountAsync());Assert.Equal(75,await db.CareerLevels.CountAsync());
    }
    [Fact] public async Task ExistingSqlDatabaseIsAdoptedWithoutChangingEmployeeData()
    {
        await using var root=fixture.Open();
        await root.Database.ExecuteSqlRawAsync("CREATE DATABASE career_legacy_upgrade");
        var connection=new NpgsqlConnectionStringBuilder(root.Database.GetConnectionString()){Database="career_legacy_upgrade"};
        await using var db=new IdentityTenantDbContext(new DbContextOptionsBuilder<IdentityTenantDbContext>().UseNpgsql(connection.ConnectionString).Options);
        var assembly=typeof(IdentityTenantDbContext).Assembly;
        foreach(var name in assembly.GetManifestResourceNames().Where(n=>n.StartsWith("IdentityTenant.Sql.")&&string.CompareOrdinal(n,"IdentityTenant.Sql.011")<0).Order())
        {
            using var stream=assembly.GetManifestResourceStream(name)!;using var reader=new StreamReader(stream);
            await db.Database.OpenConnectionAsync();
            await using var command=db.Database.GetDbConnection().CreateCommand();
            command.CommandText=await reader.ReadToEndAsync();await command.ExecuteNonQueryAsync();
        }
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO tenants(name,slug) VALUES('Legacy tenant','legacy');
            INSERT INTO users(tenant_id,email,display_name) SELECT id,'legacy@example.invalid','Legacy' FROM tenants;
            INSERT INTO employee_references(tenant_id,kind,name) SELECT id,'GRADE','Legacy grade' FROM tenants;
            INSERT INTO employees(tenant_id,user_id,employee_code,first_name,last_name,work_email,joining_date,employment_type,employment_status,grade_id)
            SELECT u.tenant_id,u.id,'LEGACY','Original','Employee',u.email,'2025-01-01','FULL_TIME','ACTIVE',r.id
            FROM users u JOIN employee_references r ON r.tenant_id=u.tenant_id;
            """);
        await db.Database.ExecuteSqlRawAsync("INSERT INTO employee_skills(tenant_id,employee_id,skill_name,category,proficiency,years_of_experience) SELECT tenant_id,id,'C#','Programming Language',4,3 FROM employees");
        await db.Database.MigrateAsync();
        var link=await db.Set<EmployeeSkill>().Include(x=>x.Skill).SingleAsync();
        Assert.NotNull(link.SkillId);Assert.Equal("C#",link.Skill!.Name);Assert.Equal(4,link.Proficiency);
        var e=await db.Set<Employee>().SingleAsync();
        Assert.Equal("Original",e.FirstName);Assert.Equal("LEGACY",e.EmployeeCode);
        Assert.NotNull(e.GradeId);Assert.Null(e.CareerProfileId);Assert.Null(e.CareerLevelId);
        Assert.Equal(31,await db.CareerProfiles.CountAsync());
        
        using var sqlStream=assembly.GetManifestResourceStream("IdentityTenant.Sql.019_employee_skill_master.sql")!;
        using var sqlReader=new StreamReader(sqlStream);await db.Database.ExecuteSqlRawAsync(await sqlReader.ReadToEndAsync());
        await db.Database.MigrateAsync();
        Assert.Equal(75,await db.CareerLevels.CountAsync());
    }
    [Fact] public async Task CatalogConstraintAndIndexNamesMatchEfModel()
    {
        await using var db=fixture.Open();
        var actualConstraints=await db.Database.SqlQueryRaw<string>("SELECT conname::text AS \"Value\" FROM pg_constraint").ToListAsync();
        var actualIndexes=await db.Database.SqlQueryRaw<string>("SELECT indexname::text AS \"Value\" FROM pg_indexes WHERE schemaname='public'").ToListAsync();
        foreach(var table in db.Model.GetRelationalModel().Tables.Where(t=>new[]{"career_profiles","grades","career_levels","skills","career_level_skills"}.Contains(t.Name)))
        {
            foreach(var key in table.UniqueConstraints)Assert.Contains(key.Name,actualConstraints);
            foreach(var key in table.ForeignKeyConstraints)Assert.Contains(key.Name,actualConstraints);
            foreach(var index in table.Indexes)Assert.Contains(index.Name,actualIndexes);
        }
    }

}

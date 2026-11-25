const db = db.getSiblingDB("bug_fix_db");

const collections = [
    "bug_fix_jobs",
    "bug_signals",
    "investigations",
    "root_cause_analyses",
    "fix_attempts",
    "changed_files",
    "test_runs",
    "fix_branches",
    "fix_commits",
    "fix_pull_requests",
    "execution_context"
];

for (const collectionName of collections) {
    if (!db.getCollectionNames().includes(collectionName)) {
        db.createCollection(collectionName);
    }
}






db.bug_fix_jobs.createIndex(
    { tenantId: 1, jobId: 1 },
    { unique: true }
);

db.bug_fix_jobs.createIndex(
    { tenantId: 1, status: 1, createdAt: -1 }
);

db.bug_fix_jobs.createIndex(
    { tenantId: 1, repositoryId: 1, createdAt: -1 }
);

db.bug_fix_jobs.createIndex(
    { tenantId: 1, workflowRunId: 1 }
);






db.bug_signals.createIndex(
    { tenantId: 1, jobId: 1, createdAt: 1 }
);

db.bug_signals.createIndex(
    { tenantId: 1, workflowRunId: 1 }
);

db.bug_signals.createIndex(
    { tenantId: 1, commitSha: 1 }
);






db.investigations.createIndex(
    { tenantId: 1, jobId: 1, attemptNumber: 1 },
    { unique: true }
);

db.investigations.createIndex(
    { tenantId: 1, jobId: 1, createdAt: -1 }
);

db.investigations.createIndex(
    { tenantId: 1, status: 1, createdAt: -1 }
);






db.root_cause_analyses.createIndex(
    { tenantId: 1, jobId: 1, attemptNumber: 1 },
    { unique: true }
);

db.root_cause_analyses.createIndex(
    { tenantId: 1, jobId: 1, confidence: -1 }
);






db.fix_attempts.createIndex(
    { tenantId: 1, jobId: 1, attemptNumber: 1 },
    { unique: true }
);

db.fix_attempts.createIndex(
    { tenantId: 1, jobId: 1, createdAt: 1 }
);

db.fix_attempts.createIndex(
    { tenantId: 1, status: 1, createdAt: -1 }
);






db.changed_files.createIndex(
    { tenantId: 1, jobId: 1, attemptNumber: 1 }
);

db.changed_files.createIndex(
    { tenantId: 1, jobId: 1, path: 1 }
);






db.test_runs.createIndex(
    { tenantId: 1, jobId: 1, attemptNumber: 1, startedAt: -1 }
);

db.test_runs.createIndex(
    { tenantId: 1, status: 1, startedAt: -1 }
);

db.test_runs.createIndex(
    { tenantId: 1, commitSha: 1 }
);






db.fix_branches.createIndex(
    { tenantId: 1, jobId: 1 },
    { unique: true }
);

db.fix_branches.createIndex(
    { tenantId: 1, repositoryId: 1, branchName: 1 },
    { unique: true }
);






db.fix_commits.createIndex(
    { tenantId: 1, jobId: 1, attemptNumber: 1 }
);

db.fix_commits.createIndex(
    { tenantId: 1, commitSha: 1 }
);






db.fix_pull_requests.createIndex(
    { tenantId: 1, jobId: 1 },
    { unique: true }
);

db.fix_pull_requests.createIndex(
    { tenantId: 1, githubPullRequestId: 1 },
    { unique: true, sparse: true }
);

db.fix_pull_requests.createIndex(
    { tenantId: 1, status: 1, createdAt: -1 }
);






db.execution_context.createIndex(
    { tenantId: 1, jobId: 1 },
    { unique: true }
);

db.execution_context.createIndex(
    { tenantId: 1, correlationId: 1 }
);

db.execution_context.createIndex(
    { tenantId: 1, updatedAt: -1 }
);


print("bug_fix_db schema/indexes created successfully.");
import { useAuth } from '../features/auth/context';
import { useUserQuery } from '../services/api/api';
import { userLabel } from '../utils/format';
import EmployeePhoto from '../features/employees/EmployeePhoto';
function Person({ user, id }) {
  const name = user?.displayName || user?.name || user?.email || userLabel(id);
  const initials = (user?.displayName || user?.name || user?.email || '?')
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map((part) => part[0])
    .join('')
    .toUpperCase();
  return (
    <span className="user-identity" title={user?.email || name}>
      {user?.profilePhotoUrl ? (
        <EmployeePhoto
          src={user.profilePhotoUrl}
          alt=""
          className="mini-avatar mini-avatar-img"
          style={{ width: '20px', height: '20px', borderRadius: '50%', objectFit: 'cover', flexShrink: 0 }}
        />
      ) : (
        <span className="mini-avatar" aria-hidden="true">
          {initials}
        </span>
      )}
      <span>
        <span>{name}</span>
        {/* {user?.email && <small>{user.email}</small>} */}
      </span>
    </span>
  );
}
function DirectoryPerson({ id }) {
  const { data } = useUserQuery(id);
  return <Person id={id} user={data} />;
}
export default function UserIdentity({ id }) {
  const { session, can } = useAuth();
  if (id == null) return <span className="muted">Unassigned</span>;
  if (String(id) === String(session.user.id)) return <Person id={id} user={session.user} />;
  return session.user.role === 'ADMIN' && can('USER_VIEW') ? (
    <DirectoryPerson id={id} />
  ) : (
    <Person id={id} />
  );
}

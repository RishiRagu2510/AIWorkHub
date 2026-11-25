import { useEffect, useState } from 'react';
import { api } from '../../../src/services/api/api';
import { useAuth } from '../../../src/features/auth/context';
import {
  PageHeader,
  QueryState,
  ErrorState,
  Pagination,
  ConfirmDialog,
} from '../../../src/components/common';
import { liveQueryOptions } from '../../../src/services/api/live';
const invitationsApi = api.enhanceEndpoints({ addTagTypes: ['Invitations'] }).injectEndpoints({
  endpoints: (b) => ({
    invitations: b.query({
      query: (page) => ({ path: '/api/invitations', params: { page } }),
      transformResponse: (d) => ({ ...d, totalPages: Math.ceil(d.totalCount / d.pageSize) }),
      providesTags: ['Invitations'],
    }),
    inviteMember: b.mutation({
      query: (body) => ({ path: '/api/invitations', method: 'POST', body }),
      invalidatesTags: (_, e) => (e ? [] : ['Invitations']),
    }),
    revokeInvitation: b.mutation({
      query: (id) => ({ path: `/api/invitations/${id}`, method: 'DELETE' }),
      invalidatesTags: (_, e) => (e ? [] : ['Invitations']),
    }),
  }),
});
export default function Invitations() {
  const { session, can } = useAuth();
  const allowed = session.user.role === 'ADMIN' && can('USER_VIEW');
  const [page, setPage] = useState(1);
  const [now, setNow] = useState(Date.now);
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 30000);
    return () => clearInterval(timer);
  }, []);
  const [email, setEmail] = useState('');
  const [days, setDays] = useState(7);
  const [pending, setPending] = useState(null);
  const [success, setSuccess] = useState('');
  const query = invitationsApi.useInvitationsQuery(page, { ...liveQueryOptions, skip: !allowed });
  const [invite, creation] = invitationsApi.useInviteMemberMutation();
  const [revoke, revoking] = invitationsApi.useRevokeInvitationMutation();
  if (!allowed) return <p>Organization invitations require administrator access.</p>;
  async function submit(e) {
    e.preventDefault();
    if (creation.isLoading) return;
    setSuccess('');
    try {
      await invite({ email: email.trim(), expiresInDays: Number(days) }).unwrap();
      setEmail('');
      setSuccess(
        'Invitation created. Ask this person to sign in with the invited Google account. No email has been sent.',
      );
    } catch {
      /* Keep input and show error. */
    }
  }
  return (
    <>
      <PageHeader
        title="Organization invitations"
        description="Invite people to your organization as developers. Their verified Google email must match the invitation."
      />
      <section className="panel collaboration-panel">
        <form onSubmit={submit}>
          <label>
            Invitee email
            <input
              name="email"
              type="email"
              required
              maxLength={320}
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
          </label>
          <label>
            Expires in days
            <input
              name="expiresInDays"
              type="number"
              min="1"
              max="30"
              required
              value={days}
              onChange={(e) => setDays(e.target.value)}
            />
          </label>
          <button className="primary" disabled={creation.isLoading}>
            {creation.isLoading ? 'Creating…' : 'Create invitation'}
          </button>
        </form>
        <ErrorState error={creation.error} />
        {success && <p role="status">{success}</p>}
      </section>
      <section className="panel collaboration-panel">
        <QueryState query={query}>
          <div
            className="table-scroll"
            tabIndex={0}
            role="region"
            aria-label="Organization invitations"
          >
            <table>
              <thead>
                <tr>
                  <th>Email</th>
                  <th>Role</th>
                  <th>Status</th>
                  <th>Expires</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {query.data?.items.map((i) => {
                  const status = i.acceptedAt
                    ? 'Accepted'
                    : i.revokedAt
                      ? 'Revoked'
                      : Date.parse(i.expiresAt) <= now
                        ? 'Expired'
                        : 'Pending';
                  return (
                    <tr key={i.id}>
                      <td>{i.email}</td>
                      <td>{i.roleCode}</td>
                      <td>{status}</td>
                      <td>{new Date(i.expiresAt).toLocaleString()}</td>
                      <td>
                        {status === 'Pending' && (
                          <button
                            onClick={() => {
                              revoking.reset();
                              setPending(i);
                            }}
                          >
                            Revoke
                          </button>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          <Pagination data={query.data} onPage={setPage} busy={query.isFetching} />
        </QueryState>
      </section>
      {pending && (
        <ConfirmDialog
          title="Revoke invitation?"
          close={() => setPending(null)}
          confirmLabel="Revoke invitation"
          busy={revoking.isLoading}
          error={revoking.error}
          confirm={async () => {
            try {
              await revoke(pending.id).unwrap();
              setPending(null);
            } catch {
              /* Keep dialog. */
            }
          }}
        >
          <p>{pending.email} will no longer be able to join using this invitation.</p>
        </ConfirmDialog>
      )}
    </>
  );
}

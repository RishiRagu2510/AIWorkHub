export function assignmentError(error) {
  if (!error || error.code === 'INVALID_VERSION') return error;
  const messages = {
    400: 'The assignee is not eligible for this project. Choose an active company user with project access.',
    503: 'Assignment eligibility could not be verified. Please retry when the user service is available.',
  };
  return { ...error, message: messages[error.status] || error.message };
}

// Membership is descriptive; the eligibility endpoint checks the active account separately.
export async function loadAssignees(
  { projectIds, selfId, extraId, includeProfiles = true },
  baseQuery,
) {
  const ids = [...new Set(projectIds.map(String))];
  if (!ids.length) return { data: [] };
  const memberships = [];
  for (const id of ids) {
    const result = await baseQuery({ path: `/api/projects/${id}/assignment-members` });
    if (result.error) return result;
    memberships.push(new Set(result.data.map((member) => String(member.userId))));
  }
  const candidates = [
    ...new Set([
      ...[...memberships[0]].filter((id) => memberships.every((members) => members.has(id))),
      ...(selfId ? [String(selfId)] : []),
      ...(extraId ? [String(extraId)] : []),
    ]),
  ];
  const people = [];
  // Bound concurrent directory calls, including for large project memberships.
  for (let offset = 0; offset < candidates.length; offset += 8) {
    const results = await Promise.all(
      candidates.slice(offset, offset + 8).map(async (id) => {
        const eligible = await baseQuery({ path: `/api/users/${id}/assignment-eligibility` });
        if (eligible.error) return eligible.error.status === 404 ? { data: null } : eligible;
        if (!eligible.data.isAdmin && !memberships.every((members) => members.has(id)))
          return { data: null };
        const profile = includeProfiles
          ? await baseQuery({ path: `/api/users/${id}` })
          : { data: null };
        if (profile.error && profile.error.status !== 404) return profile;
        return {
          data: {
            id: Number(id),
            displayName: profile.data?.displayName || eligible.data.displayName || `User #${id}`,
            email: profile.data?.email || '',
          },
        };
      }),
    );
    const failure = results.find((result) => result.error);
    if (failure) return failure;
    people.push(...results.map((result) => result.data).filter(Boolean));
  }
  return { data: people };
}

export function loginErrorMessage(code) {
  if (code === 'ORGANIZATION_ACCESS_DENIED')
    return 'Use the Google account for your registered organization. If its email domain is not registered, ask an administrator for an invitation.';
  if (code === 'ORGANIZATION_SELECTION_REQUIRED')
    return 'This account is linked to multiple organizations. Contact your administrator to resolve the membership.';
  if (code === 'ROLE_ASSIGNMENT_PENDING')
    return 'Your account is awaiting role assignment. Ask your administrator to assign a role, then sign in again.';
  if (code === 'USER_NOT_PROVISIONED')
    return 'Your account role has not been configured. Contact your administrator.';
  if (code === 'EMAIL_NOT_VERIFIED') return 'Your Google email could not be verified.';
  if (code === 'GOOGLE_SIGN_IN_FAILED') {
    return 'We could not verify your Google sign-in session. Please try signing in again.';
  }
  return code === 'COMPANY_EMAIL_NOT_ALLOWED'
    ? 'Your Google account is not associated with an authorized company.'
    : 'Unable to sign in with this Google account. Please contact your administrator.';
}

import { apiBase } from '../../services/api/client';
const errors = {
  ROLE_ASSIGNMENT_PENDING: 'Your account is awaiting role assignment. Ask your administrator to assign a role, then sign in again.',
  INVALID_CREDENTIALS:
    'Email or password is incorrect. After repeated failed attempts, wait 15 minutes before trying again.',
  INVALID_VERIFICATION:
    'This verification link is invalid, expired, or already used. Request a new link from the registration form.',
  EMAIL_UNAVAILABLE:
    'Verification email delivery is unavailable. Please try again later or use Google sign-in.',
};
export async function passwordRequest(action, body) {
  let response;
  try {
    response = await fetch(`${apiBase}/api/auth/password/${action}`, {
      method: 'POST',
      credentials: 'include',
      redirect: 'error',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'application/json',
        'ngrok-skip-browser-warning': 'true',
      },
      body: JSON.stringify(body),
    });
  } catch {
    throw new Error('Unable to connect. Please check your connection and try again.');
  }
  if (!response.ok) {
    const result = await response.json().catch(() => ({}));
    throw new Error(
      errors[result.code] ||
        (response.status === 429
          ? 'Too many attempts. Please wait a minute before trying again.'
          : response.status === 400
            ? 'Check your details and try again.'
            : 'Unable to complete sign-in. Please try again later.'),
    );
  }
}

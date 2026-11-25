export function versionHeader(version) {
  if (version == null)
    throw {
      status: 428,
      code: 'VERSION_REQUIRED',
      message: 'The loaded resource has no version. Reload it before making changes.',
    };
  if (!/^[1-9]\d*$/.test(String(version)) || !Number.isSafeInteger(Number(version)))
    throw {
      status: 400,
      code: 'INVALID_VERSION',
      message: 'The resource version is invalid. Reload it before making changes.',
    };
  return `"${version}"`;
}
export const versionMessages = {
  VERSION_REQUIRED: 'The loaded resource has no version. Reload it before making changes.',
  INVALID_VERSION: 'The resource version is invalid. Reload it before making changes.',
  STALE_VERSION:
    'Someone changed this resource. Your draft is preserved. Review the latest values before saving again.',
};

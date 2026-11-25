import { useAuth } from './context';

// Optional features a company's plan can include, separately from the portals.
export const ATHENNA = 'athenna'; // the AI chat and every AI assist inside the portals

// A context without the check (for example a test provider) leaves the feature on, like a company with no plan setting.
export function useFeature(feature) {
  const auth = useAuth();
  return auth?.canUseFeature ? auth.canUseFeature(feature) : true;
}

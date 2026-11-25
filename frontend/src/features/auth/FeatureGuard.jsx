import { useFeature } from './features';

// Shows its children only when the company's plan includes the feature.
export function FeatureGuard({ feature, children, fallback = null }) {
  return useFeature(feature) ? children : fallback;
}

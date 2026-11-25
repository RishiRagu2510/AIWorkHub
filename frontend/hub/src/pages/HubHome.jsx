import React from 'react';
import AthennaWorkspaceChat from '../../../src/components/AthennaWorkspaceChat';
import PlanRestricted from '../../../src/components/PlanRestricted';
import { FeatureGuard } from '../../../src/features/auth/FeatureGuard';
import { ATHENNA } from '../../../src/features/auth/features';
import '../../../src/styles/hub-home.scss';

export default function HubHome() {
  return (
    <div className="hub-home-page" aria-label="AIWorkHub Autonomous Workspace Copilot">
      <FeatureGuard feature={ATHENNA} fallback={<PlanRestricted feature={ATHENNA} />}>
        <AthennaWorkspaceChat />
      </FeatureGuard>
    </div>
  );
}

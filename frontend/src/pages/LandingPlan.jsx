import LandingShell from '../components/LandingShell';
import PlanPage from './PlanPage';

/** The company's plan, managed on the landing site itself: the same page every portal has, so nobody is sent to GridHub to do it. */
export default function LandingPlan() {
  return (
    <LandingShell back>
      <PlanPage />
    </LandingShell>
  );
}

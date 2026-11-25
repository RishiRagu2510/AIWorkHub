import {useParams} from 'react-router';
import OnboardingReview from '../../../src/features/employees/OnboardingReview';
import Link from '../../../src/components/PortalLink';
export default function EmployeeOnboarding({ completing = false }) {
 const {issueId}=useParams();
 return <><header className="page-header"><h1>{completing ? 'Add employee' : 'Employee onboarding review'}</h1><Link to={`/issues/${issueId}`}>Open Jira ticket</Link></header><OnboardingReview id={issueId} standalone completing={completing}/></>;
}

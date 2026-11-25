import { lazy, Suspense, useEffect, useRef } from 'react';
import Link from '../../components/PortalLink';
import { LoadingState } from '../../components/common';
import { useUnsavedChanges } from '../../components/unsavedContext';
const IssueDetails = lazy(() => import('../../pages/IssueDetails'));
export default function IssuePreview({ id, close }) {
  const dialog = useRef(null);
  const { confirmLeave } = useUnsavedChanges();
  useEffect(() => {
    const previous = document.activeElement;
    dialog.current.showModal();
    const element = dialog.current;
    return () => {
      element.close();
      previous?.focus();
    };
  }, []);
  return (
    <dialog
      className="issue-preview"
      ref={dialog}
      aria-label="Issue preview"
      onCancel={(event) => {
        event.preventDefault();
        confirmLeave(close);
      }}
    >
      <div className="section-header">
        <h2>Issue preview</h2>
        <div className="actions">
          <Link to={`/issues/${id}`}>Open full page</Link>
          <button onClick={() => confirmLeave(close)} aria-label="Close issue preview">
            Close
          </button>
        </div>
      </div>
      <div className="preview-body">
        <Suspense fallback={<LoadingState />}>
          <IssueDetails selectedId={id} />
        </Suspense>
      </div>
    </dialog>
  );
}

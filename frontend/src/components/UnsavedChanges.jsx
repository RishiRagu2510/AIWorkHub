import { useEffect, useRef, useState } from 'react';
import { useBlocker } from 'react-router';
import { ConfirmDialog } from './common';
import { DirtyContext } from './unsavedContext';
export function UnsavedChangesProvider({ children }) {
  const forms = useRef(new Set());
  const [action, setAction] = useState(null);
  const hasChanges = () => [...forms.current].some((dirty) => dirty.current);
  const blocker = useBlocker(() => hasChanges());
  const [context] = useState(() => ({
    register: (dirty) => {
      forms.current.add(dirty);
      return () => forms.current.delete(dirty);
    },
    confirmLeave: (next) => {
      if ([...forms.current].some((dirty) => dirty.current)) setAction(() => next);
      else next();
    },
  }));
  useEffect(() => {
    function beforeUnload(event) {
      if ([...forms.current].some((dirty) => dirty.current)) {
        event.preventDefault();
        event.returnValue = '';
      }
    }
    window.addEventListener('beforeunload', beforeUnload);
    return () => window.removeEventListener('beforeunload', beforeUnload);
  }, []);
  function cancel() {
    setAction(null);
    if (blocker.state === 'blocked') blocker.reset();
  }
  function discard() {
    forms.current.forEach((dirty) => {
      dirty.current = false;
    });
    if (action) {
      action();
      setAction(null);
    } else if (blocker.state === 'blocked') blocker.proceed();
  }
  return (
    <DirtyContext.Provider value={context}>
      {children}
      {(action || blocker.state === 'blocked') && (
        <ConfirmDialog
          title="Discard unsaved changes?"
          confirmLabel="Discard changes"
          confirmClass="danger"
          close={cancel}
          confirm={discard}
        >
          <p>
            Your edits have not been saved. Stay on this page to keep editing, or discard them to
            continue.
          </p>
        </ConfirmDialog>
      )}
    </DirtyContext.Provider>
  );
}

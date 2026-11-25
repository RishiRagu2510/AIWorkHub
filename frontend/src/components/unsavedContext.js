import { createContext, useContext, useEffect, useRef } from 'react';
export const DirtyContext = createContext(null);
export function useUnsavedChanges() {
  const context = useContext(DirtyContext);
  const dirty = useRef(false);
  useEffect(() => context?.register(dirty), [context]);
  return {
    markDirty: () => {
      dirty.current = true;
    },
    clearDirty: () => {
      dirty.current = false;
    },
    confirmLeave: (action) => (context ? context.confirmLeave(action) : action()),
  };
}

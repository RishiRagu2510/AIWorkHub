import { useEffect, useMemo } from 'react';
import { useSearchParams } from 'react-router';
import { useAuth } from '../features/auth/context';
import { preferenceKey, readPreference, writePreference } from './preferences';

export function useSavedFilters(name, allowed) {
  const { session } = useAuth();
  const key = preferenceKey(session.user, `filters:${name}`);
  const [search, setSearch] = useSearchParams();
  const saved = useMemo(
    () =>
      search.toString()
        ? {}
        : Object.fromEntries(
            Object.entries(readPreference(key)).filter(
              ([field, value]) => allowed.includes(field) && typeof value === 'string',
            ),
          ),
    [key, allowed, search],
  );
  // An explicit URL replaces saved criteria instead of merging unrelated filters.
  const effective = search.toString() ? search : new URLSearchParams(saved);
  useEffect(() => {
    if (!search.toString()) setSearch({ ...saved, filters: '1' }, { replace: true });
    else
      writePreference(
        key,
        Object.fromEntries([...search].filter(([field]) => allowed.includes(field))),
      );
  }, [search, key, setSearch, allowed, saved]);
  function update(next) {
    const params = new URLSearchParams(next);
    // Distinguish an intentional reset from a fresh visit.
    params.set('filters', '1');
    setSearch(params);
    writePreference(
      key,
      Object.fromEntries([...params].filter(([field]) => allowed.includes(field))),
    );
  }
  return [effective, update];
}

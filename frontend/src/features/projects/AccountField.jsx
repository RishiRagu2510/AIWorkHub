import { useId, useState, useRef, useEffect } from 'react';
import { useAccountsQuery } from '../../services/api/api';
import { Field } from '../../components/common';

export default function AccountField({ initialName = '', projects = [] }) {
  const listId = useId();
  const [name, setName] = useState(initialName);
  const [open, setOpen] = useState(false);
  const dropdownRef = useRef(null);
  const inputRef = useRef(null);
  const query = useAccountsQuery();

  const accounts =
    query.data?.filter((a) => a.status !== 'ARCHIVED') ||
    projects.map((project) => ({
      id: project.accountId,
      name: project.accountName || project.name,
    }));
  const unique = [
    ...new Map(accounts.map((account) => [account.name.toLocaleLowerCase(), account])).values(),
  ];
  const selected = unique.find(
    (account) => account.name.toLocaleLowerCase() === name.trim().toLocaleLowerCase(),
  );

  const filtered = name.trim()
    ? unique.filter((account) =>
        account.name.toLocaleLowerCase().includes(name.trim().toLocaleLowerCase()),
      )
    : unique;

  useEffect(() => {
    function handleClickOutside(e) {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target)) {
        setOpen(false);
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const handleSelectAccount = (accountName) => {
    setName(accountName);
    setOpen(false);
    if (inputRef.current) {
      inputRef.current.value = accountName;
      inputRef.current.dispatchEvent(new Event('input', { bubbles: true }));
      inputRef.current.dispatchEvent(new Event('change', { bubbles: true }));
    }
  };

  return (
    <div className="account-field" ref={dropdownRef}>
      <div className="account-field-input-wrap">
        <Field
          label="Account *"
          name="name"
          value={name}
          required
          maxLength={200}
          placeholder="Select or enter an account name"
          aria-describedby={`${listId}-hint`}
          autoComplete="off"
          ref={inputRef}
          onFocus={() => setOpen(true)}
          onChange={(event) => {
            setName(event.target.value);
            setOpen(true);
          }}
          onKeyDown={(e) => {
            if (e.key === 'Escape') setOpen(false);
          }}
        />
        {open && filtered.length > 0 && (
          <div className="custom-combobox-dropdown" role="listbox" aria-label="Suggested accounts">
            <div className="combobox-header-label">Existing accounts ({filtered.length})</div>
            {filtered.map((account) => {
              const isCurrent =
                account.name.toLocaleLowerCase() === name.trim().toLocaleLowerCase();
              return (
                <div
                  key={account.name}
                  className={`combobox-option ${isCurrent ? 'is-selected' : ''}`}
                  role="option"
                  aria-selected={isCurrent}
                  onMouseDown={(e) => {
                    e.preventDefault();
                    handleSelectAccount(account.name);
                  }}
                >
                  <span className="combobox-option-icon" aria-hidden="true">
                    🏢
                  </span>
                  <span className="combobox-option-text">{account.name}</span>
                  {isCurrent && (
                    <svg
                      className="combobox-check"
                      width="14"
                      height="14"
                      viewBox="0 0 24 24"
                      fill="none"
                      stroke="currentColor"
                      strokeWidth="2.5"
                    >
                      <polyline points="20 6 9 17 4 12" />
                    </svg>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </div>

      <input type="hidden" name="accountId" value={selected?.id || ''} />
      <p
        className={`hint account-hint-card ${selected ? 'is-existing' : 'is-new'}`}
        id={`${listId}-hint`}
      >
        <span className="hint-pill-icon" aria-hidden="true">
          {selected ? '✓' : '✦'}
        </span>
        <span>
          {selected
            ? 'This project will belong to the existing account.'
            : 'Choose an existing account or enter a new one. An account can have multiple projects.'}
        </span>
      </p>
      {query.isError && (
        <p className="hint">
          Could not load all accounts.{' '}
          <button type="button" className="text-button" onClick={query.refetch}>
            Retry accounts
          </button>
        </p>
      )}
    </div>
  );
}

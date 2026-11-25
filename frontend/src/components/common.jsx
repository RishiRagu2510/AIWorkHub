import { NoticeContext } from './noticeContext';
import { Fragment, useEffect, useId, useRef, useState } from 'react';
import { label } from '../utils/format';
import CustomDatePicker from './CustomDatePicker';
export function PageHeader({ eyebrow, title, description, children }) {
  return (
    <header className="page-header">
      <div>
        {eyebrow && <div className="eyebrow">{eyebrow}</div>}
        {title && <h1>{title}</h1>}
        {description && <p>{description}</p>}
      </div>
      <div className="actions">{children}</div>
    </header>
  );
}
export function LoadingState() {
  return (
    <div className="loading" role="status">
      <span className="sr-only">Loading workspace data…</span>
      <span className="skeleton-lines" aria-hidden="true">
        <i />
        <i />
        <i />
      </span>
    </div>
  );
}
function EmptyIllustration({ variant }) {
  if (variant === 'issues') {
    return (
      <svg className="empty-illustration" width="72" height="72" viewBox="0 0 72 72" fill="none" aria-hidden="true">
        <rect x="16" y="12" width="40" height="50" rx="6" stroke="#f59e0b" strokeWidth="2.5" fill="#fef3c7" fillOpacity="0.4" />
        <rect x="26" y="8" width="20" height="8" rx="3" stroke="#f59e0b" strokeWidth="2" fill="#fff" />
        <circle cx="36" cy="12" r="2" fill="#f59e0b" />
        <line x1="24" y1="26" x2="48" y2="26" stroke="#f59e0b" strokeWidth="2.5" strokeLinecap="round" />
        <line x1="24" y1="36" x2="44" y2="36" stroke="#94a3b8" strokeWidth="2" strokeLinecap="round" />
        <line x1="24" y1="46" x2="38" y2="46" stroke="#94a3b8" strokeWidth="2" strokeLinecap="round" />
        <circle cx="48" cy="46" r="3" fill="#10b981" />
      </svg>
    );
  }
  if (variant === 'projects') {
    return (
      <svg className="empty-illustration" width="72" height="72" viewBox="0 0 72 72" fill="none" aria-hidden="true">
        <path d="M12 20C12 17.79 13.79 16 16 16H28L34 22H56C58.21 22 60 23.79 60 26V52C60 54.21 58.21 56 56 56H16C13.79 56 12 54.21 12 52V20Z" stroke="#f59e0b" strokeWidth="2.5" fill="#fef3c7" fillOpacity="0.4" />
        <line x1="22" y1="34" x2="42" y2="34" stroke="#f59e0b" strokeWidth="2.5" strokeLinecap="round" />
        <line x1="22" y1="42" x2="34" y2="42" stroke="#94a3b8" strokeWidth="2" strokeLinecap="round" />
      </svg>
    );
  }
  if (variant === 'search') {
    return (
      <svg className="empty-illustration" width="72" height="72" viewBox="0 0 72 72" fill="none" aria-hidden="true">
        <circle cx="32" cy="32" r="18" stroke="#f59e0b" strokeWidth="2.5" fill="#fef3c7" fillOpacity="0.3" />
        <line x1="45" y1="45" x2="58" y2="58" stroke="#f59e0b" strokeWidth="3" strokeLinecap="round" />
        <circle cx="28" cy="28" r="4" fill="#f59e0b" fillOpacity="0.6" />
      </svg>
    );
  }
  if (variant === 'notifications') {
    return (
      <svg className="empty-illustration" width="72" height="72" viewBox="0 0 72 72" fill="none" aria-hidden="true">
        <path d="M36 12C28.27 12 22 18.27 22 26V38L16 46H56L50 38V26C50 18.27 43.73 12 36 12Z" stroke="#f59e0b" strokeWidth="2.5" fill="#fef3c7" fillOpacity="0.4" />
        <path d="M31 50C31 52.76 33.24 55 36 55C38.76 55 41 52.76 41 50" stroke="#f59e0b" strokeWidth="2.5" strokeLinecap="round" />
        <circle cx="36" cy="10" r="3" fill="#f59e0b" />
      </svg>
    );
  }
  if (variant === 'timesheets') {
    return (
      <svg className="empty-illustration" width="72" height="72" viewBox="0 0 72 72" fill="none" aria-hidden="true">
        <circle cx="36" cy="36" r="22" stroke="#f59e0b" strokeWidth="2.5" fill="#fef3c7" fillOpacity="0.4" />
        <polyline points="36,24 36,36 44,40" stroke="#f59e0b" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
        <circle cx="36" cy="36" r="2" fill="#f59e0b" />
      </svg>
    );
  }
  return (
    <svg className="empty-illustration" width="72" height="72" viewBox="0 0 72 72" fill="none" aria-hidden="true">
      <rect x="18" y="18" width="36" height="36" rx="8" stroke="#f59e0b" strokeWidth="2.5" fill="#fef3c7" fillOpacity="0.3" />
      <path d="M26 30H46M26 38H40M26 46H34" stroke="#94a3b8" strokeWidth="2" strokeLinecap="round" />
    </svg>
  );
}

export function EmptyState({ title = 'No data available', children, variant = 'default' }) {
  return (
    <div className="empty">
      <EmptyIllustration variant={variant} />
      <h3>{title}</h3>
      {children && <p>{children}</p>}
    </div>
  );
}
export function ErrorState({ error, retry }) {
  return error ? (
    <div className="error" role="alert">
      <strong>Unable to complete this request</strong>
      <p>{error.message || 'Please try again.'}</p>
      {retry && (
        <button type="button" onClick={retry}>
          Try again
        </button>
      )}
    </div>
  ) : null;
}
export function QueryState({ query, children }) {
  if (query.isLoading || query.isUninitialized) return <LoadingState />;
  if (query.error && (!query.data || [401, 403, 404].includes(query.error.status)))
    return <ErrorState error={query.error} retry={query.refetch} />;
  return (
    <div aria-busy={query.isFetching}>
      <ErrorState error={query.error} retry={query.refetch} />
      {children}
    </div>
  );
}
export function Badge({ value }) {
  return <span className={`badge badge-${String(value).toLowerCase()}`}>{label(value)}</span>;
}
export function Field({ label: title, name, children, ...props }) {
  const id = useId();
  return (
    <label className="field">
      <span>{title}</span>
      {children || (props.type === 'date' ? (
        <CustomDatePicker id={id} name={name} aria-label={props['aria-label'] || title} {...props} />
      ) : (
        <input id={id} name={name} {...props} />
      ))}
    </label>
  );
}
export function SelectField({ label: title, name, values = [], empty, ...props }) {
  const options = values.map((v) => {
    if (typeof v === 'object' && v !== null) {
      return { value: String(v.value), label: v.label };
    }
    return { value: String(v), label: label(v) };
  });

  return (
    <CustomSelect
      label={title}
      name={name}
      options={options}
      placeholder={empty != null ? empty : 'Select option'}
      allowEmpty={empty != null}
      value={props.value}
      defaultValue={props.defaultValue}
      onChange={props.onChange}
      required={props.required}
      disabled={props.disabled}
      aria-label={props['aria-label']}
      className={props.className}
    />
  );
}

export function CustomSelect({
  label: title,
  name,
  value,
  defaultValue,
  onChange,
  options = [],
  placeholder = 'Select an option',
  required = false,
  allowEmpty = true,
  disabled = false,
}) {
  const [open, setOpen] = useState(false);
  const containerRef = useRef(null);
  const selectRef = useRef(null);
  const [internalVal, setInternalVal] = useState(defaultValue || '');
  const currentValue = value !== undefined ? value : internalVal;

  const sanitizedOptions = options.filter(
    (opt) => opt && opt.value !== '' && opt.value !== undefined && opt.value !== null
  );

  const selectedOption = sanitizedOptions.find((opt) => String(opt.value) === String(currentValue));
  const displayText = selectedOption
    ? selectedOption.label
    : (allowEmpty && placeholder ? placeholder : (sanitizedOptions[0]?.label || 'Select an option'));

  useEffect(() => {
    function handleClickOutside(e) {
      if (containerRef.current && !containerRef.current.contains(e.target)) {
        setOpen(false);
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const handleSelect = (val) => {
    setOpen(false);
    setInternalVal(val);
    if (selectRef.current) {
      selectRef.current.value = val;
      selectRef.current.dispatchEvent(new Event('change', { bubbles: true }));
    }
    onChange?.({ target: { name, value: val } });
  };

  const hiddenSelect = (
    <select
      ref={selectRef}
      name={name}
      value={currentValue || ''}
      disabled={disabled}
      required={required}
      onInvalid={() => containerRef.current?.querySelector('button')?.focus()}
      onChange={(e) => {
        setInternalVal(e.target.value);
        onChange?.(e);
      }}
      className="visually-hidden-select"
      aria-label={title || placeholder || 'Select option'}
    >
      {allowEmpty && placeholder && <option value="">{placeholder}</option>}
      {sanitizedOptions.map((opt) => (
        <option key={opt.value} value={opt.value}>
          {opt.label}
        </option>
      ))}
    </select>
  );

  return (
    <div className={`custom-select-container${disabled ? ' is-disabled' : ''}`} ref={containerRef}>
      {title ? (
        <label className="custom-select-label-wrap">
          <span className="custom-select-label">{title}</span>
          {hiddenSelect}
        </label>
      ) : (
        hiddenSelect
      )}

      <button
        type="button"
        disabled={disabled}
        className={`custom-select-trigger ${open ? 'is-open' : ''} ${currentValue ? 'has-value' : ''}`}
        onClick={() => !disabled && setOpen(!open)}
        onKeyDown={(e) => {
          if (e.key === 'Escape') { setOpen(false); return; }
          if (e.key !== 'ArrowDown' && e.key !== 'ArrowUp') return;
          e.preventDefault();
          const choices = allowEmpty ? [{ value: '' }, ...sanitizedOptions] : sanitizedOptions;
          const index = choices.findIndex(opt => String(opt.value) === String(currentValue));
          const next = Math.max(0, Math.min(choices.length - 1, index + (e.key === 'ArrowDown' ? 1 : -1)));
          if (choices[next]) handleSelect(choices[next].value);
        }}
        aria-haspopup="listbox"
        aria-expanded={open}
      >
        <span className="custom-select-value">{displayText}</span>
        <svg
          className={`custom-select-chevron ${open ? 'rotate' : ''}`}
          width="16"
          height="16"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
        >
          <polyline points="6 9 12 15 18 9" />
        </svg>
      </button>

      {open && (
        <div className="custom-select-dropdown" role="listbox">
          {allowEmpty && placeholder && (
            <div
              className={`custom-select-option ${!currentValue ? 'is-selected' : ''}`}
              role="option"
              aria-selected={!currentValue}
              onClick={() => handleSelect('')}
            >
              <span>{placeholder}</span>
              {!currentValue && (
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
                  <polyline points="20 6 9 17 4 12" />
                </svg>
              )}
            </div>
          )}
          {sanitizedOptions.map((opt) => {
            const isSelected = String(opt.value) === String(currentValue);
            return (
              <div
                key={opt.value}
                className={`custom-select-option ${isSelected ? 'is-selected' : ''}`}
                role="option"
                aria-selected={isSelected}
                onClick={() => handleSelect(opt.value)}
              >
                <span>{opt.label}</span>
                {isSelected && (
                  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
                    <polyline points="20 6 9 17 4 12" />
                  </svg>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
export function DataTable({ columns, rows, empty = 'No results found.', emptyVariant = 'default', renderExpandedRow }) {
  if (!rows?.length)
    return <EmptyState title={empty} variant={emptyVariant}>Try changing your filters or come back later.</EmptyState>;
  return (
    <div className="table-scroll" tabIndex="0" role="region" aria-label="Scrollable data table">
      <table>
        <thead>
          <tr>
            {columns.map((c) => (
              <th key={c.key} scope="col" data-column={c.key}>
                {c.title}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => {
            const expanded = renderExpandedRow?.(row);
            return (
              <Fragment key={row.id}>
                <tr>
                  {columns.map((c) => (
                    <td key={c.key} data-label={typeof c.title === 'string' ? c.title : undefined} data-column={c.key}>
                      {c.render ? c.render(row) : (row[c.key] ?? '—')}
                    </td>
                  ))}
                </tr>
                {expanded && (
                  <tr className="expanded-detail-row">
                    <td colSpan={columns.length}>{expanded}</td>
                  </tr>
                )}
              </Fragment>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
export function Pagination({ data, onPage, busy }) {
  if (!data) return null;
  return (
    <div className="pagination">
      <span>
        {data.totalCount} results · Page {data.page} of {Math.max(1, data.totalPages)}
      </span>
      <div className="actions">
        <button disabled={busy || data.page <= 1} onClick={() => onPage(data.page - 1)}>
          ← Previous
        </button>
        <button
          disabled={busy || data.page >= data.totalPages}
          onClick={() => onPage(data.page + 1)}
        >
          Next →
        </button>
      </div>
    </div>
  );
}
export function ConfirmDialog({
  title,
  children,
  confirm,
  close,
  busy,
  error,
  confirmLabel = 'Delete issue',
  busyLabel = 'Deleting…',
  confirmClass = 'danger',
  confirmDisabled = false,
}) {
  const ref = useRef(null);
  useEffect(() => {
    const dialog = ref.current;
    const prior = document.activeElement;
    dialog.showModal();
    return () => {
      dialog.close();
      prior?.focus();
    };
  }, []);
  return (
    <dialog
      ref={ref}
      aria-labelledby="dialog-title"
      onCancel={(e) => {
        e.preventDefault();
        if (!busy) close();
      }}
    >
      <h2 id="dialog-title">{title}</h2>
      {children}
      <ErrorState error={error} />
      <div className="actions">
        <button autoFocus disabled={busy} onClick={close}>
          Cancel
        </button>
        <button className={confirmClass} disabled={busy || confirmDisabled} onClick={confirm}>
          {busy ? busyLabel : confirmLabel}
        </button>
      </div>
    </dialog>
  );
}

export function NoticeProvider({ children }) {
  const [notice, setNotice] = useState('');
  useEffect(() => {
    if (notice) {
      const timer = setTimeout(() => setNotice(''), notice.type === 'error' ? 10000 : 5000);
      return () => clearTimeout(timer);
    }
  }, [notice]);
  return (
    <NoticeContext.Provider value={setNotice}>
      {children}
      <div
        className={
          notice ? `snackbar${notice.type === 'error' ? ' snackbar-error' : ''}` : 'sr-only'
        }
        role={notice.type === 'error' ? 'alert' : 'status'}
      >
        {typeof notice === 'string' ? notice : notice.message}
        {notice.type === 'error' && (
          <button aria-label="Dismiss notification" onClick={() => setNotice('')}>
            ×
          </button>
        )}
      </div>
    </NoticeContext.Provider>
  );
}
export function JsonViewer({ value }) {
  let parsed = value;
  try {
    parsed = JSON.parse(value);
  } catch {
    /* Non-JSON backend text is displayed safely. */
  }
  return (
    <pre>
      {parsed == null
        ? 'No data available'
        : typeof parsed === 'string'
          ? parsed
          : JSON.stringify(parsed, null, 2)}
    </pre>
  );
}

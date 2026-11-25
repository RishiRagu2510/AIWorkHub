import Autocomplete from '@mui/material/Autocomplete';
import TextField from '@mui/material/TextField';

function initials(developer) {
  return (developer.displayName || developer.email || '?')
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map((part) => part[0])
    .join('')
    .toUpperCase();
}

export default function DeveloperPicker({
  developers,
  value,
  onChange,
  disabled,
  multiple = false,
  id = 'project-developer',
  placeholder = 'Search by name or email…',
  noOptionsText = 'No matching developers on this page.',
}) {
  const selectedValue = multiple
    ? (Array.isArray(value) ? value : value ? [value] : [])
        .map((v) => developers.find((d) => String(d.id) === String(typeof v === 'object' ? v.id : v)))
        .filter(Boolean)
    : developers.find((developer) => String(developer.id) === String(typeof value === 'object' ? value?.id : value)) || null;

  return (
    <Autocomplete
      id={id}
      className="developer-picker"
      multiple={multiple}
      options={developers}
      value={selectedValue}
      onChange={(_, newValue) => {
        if (multiple) {
          onChange(newValue ? newValue.map((item) => String(item.id)) : []);
        } else {
          onChange(newValue ? String(newValue.id) : '');
        }
      }}
      disabled={disabled}
      autoHighlight
      getOptionLabel={(developer) => developer.displayName || developer.email}
      getOptionKey={(developer) => developer.id}
      isOptionEqualToValue={(option, selected) => option.id === selected.id}
      filterOptions={(options, { inputValue }) => {
        const query = inputValue.trim().toLowerCase();
        return options.filter((developer) =>
          `${developer.displayName || ''} ${developer.email}`.toLowerCase().includes(query),
        );
      }}
      noOptionsText={noOptionsText}
      slotProps={{ popper: { className: 'developer-picker-popup' } }}
      renderInput={(params) => <TextField {...params} placeholder={placeholder} />}
      renderOption={({ key, ...props }, developer, { selected }) => (
        <li key={key} {...props}>
          <span className="developer-picker-avatar" aria-hidden="true">
            {initials(developer)}
          </span>
          <span className="developer-picker-person">
            <strong>
              {developer.displayName || developer.email}
              {developer.isSkillMatch && (
                <span style={{
                  marginLeft: '6px',
                  fontSize: '0.7rem',
                  fontWeight: 600,
                  padding: '1px 6px',
                  borderRadius: '10px',
                  background: '#fef3c7',
                  color: '#92400e'
                }}>
                  ⭐ Best Match
                </span>
              )}
            </strong>
            <small>{developer.email}</small>
          </span>
          {selected && (
            <span className="developer-picker-check" aria-hidden="true">
              ✓
            </span>
          )}
        </li>
      )}
    />
  );
}

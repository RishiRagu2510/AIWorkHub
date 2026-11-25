import { useEffect, useState } from 'react';
const choices = ['light', 'dark', 'system'];

export default function ThemeSettings({ onThemeChange }) {
  const [theme, setTheme] = useState('light');
  useEffect(() => {
    const media = window.matchMedia?.('(prefers-color-scheme: dark)');
    const apply = () => {
      const resolved = theme === 'system' ? (media?.matches ? 'dark' : 'light') : theme;
      document.documentElement.dataset.theme = resolved;
      onThemeChange?.(resolved);
    };
    apply();
    media?.addEventListener('change', apply);
    return () => {
      media?.removeEventListener('change', apply);
      delete document.documentElement.dataset.theme;
    };
  }, [theme, onThemeChange]);
  function change(value) {
    setTheme(value);
  }
  return (
    <fieldset className="theme-settings">
      <legend>Appearance</legend>
      <div className="theme-options">
        {choices.map((value) => (
          <label key={value}>
            <input
              type="radio"
              name="appearance"
              value={value}
              checked={theme === value}
              onChange={() => change(value)}
            />
            <span>{value[0].toUpperCase() + value.slice(1)}</span>
          </label>
        ))}
      </div>
      <p>Applies to this session.</p>
    </fieldset>
  );
}

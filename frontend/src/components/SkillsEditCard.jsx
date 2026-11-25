import React, { useMemo, useState } from 'react';
import { useSaveEmployeeMutation } from '../services/api/employees';

export const LEVELS = ['Beginner', 'Basic', 'Intermediate', 'Advanced', 'Expert'];

const messageOf = (error) => error?.message || error?.data?.message || 'The change was not saved.';
const yearsText = (n) => `${Number(n) || 0} ${Number(n) === 1 ? 'year' : 'years'}`;
const byName = (a, b) => a.skill.localeCompare(b.skill);

/**
 * The Update my skills form in the chat: the skills the person already has (pick one to change its level and years, or
 * remove it) and the company skill list to add from. It saves through the same endpoints, and so the same checks, as the
 * Skills tab in Persona.
 */
export default function SkillsEditCard({ form, disabled }) {
  const [save, { isLoading }] = useSaveEmployeeMutation();
  const [skills, setSkills] = useState(() => [...form.skills].sort(byName));
  const [available, setAvailable] = useState(form.available);
  const [version, setVersion] = useState(form.version);
  const [picked, setPicked] = useState(null); // { entry } to change, or { item } to add
  const [level, setLevel] = useState(0);
  const [years, setYears] = useState('0');
  const [search, setSearch] = useState('');
  const [confirmRemove, setConfirmRemove] = useState(null);
  const [error, setError] = useState(null);
  const [notice, setNotice] = useState('');

  const matches = useMemo(() => {
    const q = search.trim().toLowerCase();
    return available.filter((a) => !q || a.name.toLowerCase().includes(q) || (a.category || '').toLowerCase().includes(q));
  }, [available, search]);

  const yearsValue = Number(years);
  const problem = !picked
    ? ''
    : !level
      ? 'Choose a level'
      : !Number.isFinite(yearsValue) || yearsValue < 0 || yearsValue > 80
        ? 'Years must be between 0 and 80'
        : '';
  const busy = isLoading || disabled;

  function pickEntry(entry) {
    setPicked({ entry });
    setLevel(entry.proficiency || 0);
    setYears(String(entry.yearsOfExperience ?? 0));
    setConfirmRemove(null);
    setError(null);
    setNotice('');
  }

  function pickItem(item) {
    setPicked({ item });
    setLevel(0);
    setYears('0');
    setConfirmRemove(null);
    setError(null);
    setNotice('');
  }

  const cancel = () => {
    setPicked(null);
    setError(null);
  };

  async function submit(e) {
    e.preventDefault();
    if (!picked || problem || busy) return;
    setError(null);
    const entry = picked.entry;
    const item = picked.item;
    const skillId = entry ? entry.skillId : item.id;
    try {
      const saved = await save({
        path: entry ? `/me/skills/${entry.id}` : '/me/skills',
        method: entry ? 'PUT' : 'POST',
        version,
        body: { skillId, proficiency: level, yearsOfExperience: yearsValue, lastUsedDate: entry?.lastUsed || null },
      }).unwrap();
      if (saved?.version != null) setVersion(saved.version);
      const row = saved?.item || {};
      const next = {
        id: row.id ?? entry?.id,
        skillId,
        skill: entry ? entry.skill : item.name,
        category: entry ? entry.category : item.category,
        proficiency: level,
        yearsOfExperience: yearsValue,
        lastUsed: entry?.lastUsed || null,
      };
      setSkills((prev) => [...prev.filter((s) => s.id !== next.id), next].sort(byName));
      if (item) setAvailable((prev) => prev.filter((a) => a.id !== item.id));
      setNotice(`${entry ? 'Updated' : 'Added'} ${next.skill}: ${LEVELS[level - 1]}, ${yearsText(yearsValue)}.`);
      setPicked(null);
    } catch (err) {
      setError(messageOf(err));
    }
  }

  async function remove(entry) {
    if (busy) return;
    setError(null);
    try {
      const saved = await save({ path: `/me/skills/${entry.id}`, method: 'DELETE', version }).unwrap();
      if (saved?.version != null) setVersion(saved.version);
      setSkills((prev) => prev.filter((s) => s.id !== entry.id));
      if (entry.skillId != null) {
        setAvailable((prev) => [...prev, { id: entry.skillId, name: entry.skill, category: entry.category }].sort((a, b) => a.name.localeCompare(b.name)));
      }
      if (picked?.entry?.id === entry.id) setPicked(null);
      setConfirmRemove(null);
      setNotice(`Removed ${entry.skill}.`);
    } catch (err) {
      setError(messageOf(err));
    }
  }

  const pickedName = picked?.entry?.skill || picked?.item?.name;

  return (
    <form className="worklog-card skills-card" onSubmit={submit} aria-label="Update my skills">
      <div className="issue-create-card-header">
        <div className="issue-create-title-group">
          <span className="issue-create-title-icon">🛠️</span>
          <div className="worklog-title">Update my skills</div>
        </div>
      </div>

      <div className="worklog-label" id="my-skills-label">Your skills{skills.length > 0 ? ` (${skills.length})` : ''}</div>
      {skills.length === 0 ? (
        <p className="skills-empty">You have no skills on your profile yet. Choose one below to add it.</p>
      ) : (
        <ul className="skills-list" aria-labelledby="my-skills-label">
          {skills.map((s) => (
            <li key={s.id} className={`skills-row ${picked?.entry?.id === s.id ? 'selected' : ''}`}>
              <button
                type="button"
                className="skills-row-main"
                aria-pressed={picked?.entry?.id === s.id}
                aria-label={`Change ${s.skill}`}
                onClick={() => pickEntry(s)}
                disabled={busy}
              >
                <span className="skills-name">{s.skill}</span>
                {s.category && <span className="skills-category">{s.category}</span>}
                <span className="skills-level" aria-label={`${LEVELS[(s.proficiency || 1) - 1]}, ${yearsText(s.yearsOfExperience)}`}>
                  <span className="skills-dots" aria-hidden="true">
                    {[1, 2, 3, 4, 5].map((n) => <i key={n} className={n <= s.proficiency ? 'on' : ''} />)}
                  </span>
                  {LEVELS[(s.proficiency || 1) - 1]} · {yearsText(s.yearsOfExperience)}
                </span>
              </button>
              {confirmRemove === s.id ? (
                <span className="skills-confirm">
                  <button type="button" className="skills-btn danger" onClick={() => remove(s)} disabled={busy}>Remove</button>
                  <button type="button" className="skills-btn" onClick={() => setConfirmRemove(null)}>Keep</button>
                </span>
              ) : (
                <button
                  type="button"
                  className="skills-btn icon"
                  aria-label={`Remove ${s.skill}`}
                  title="Remove this skill"
                  onClick={() => setConfirmRemove(s.id)}
                  disabled={busy}
                >
                  ×
                </button>
              )}
            </li>
          ))}
        </ul>
      )}

      <div className="worklog-label" id="add-skill-label">Add a skill</div>
      {available.length === 0 ? (
        <p className="skills-empty">Every skill in the company list is already on your profile. Ask an administrator to add more in Employee setup.</p>
      ) : (
        <>
          <input
            className="worklog-title-input"
            type="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search skills"
            aria-label="Search skills"
          />
          <div className="skills-chips" role="group" aria-labelledby="add-skill-label">
            {matches.slice(0, 24).map((a) => (
              <button
                key={a.id}
                type="button"
                className={`skills-chip ${picked?.item?.id === a.id ? 'selected' : ''}`}
                aria-pressed={picked?.item?.id === a.id}
                onClick={() => pickItem(a)}
                disabled={busy}
              >
                {a.name}
                {a.category && <small>{a.category}</small>}
              </button>
            ))}
            {matches.length === 0 && <span className="skills-empty">No skill matches "{search}".</span>}
            {matches.length > 24 && <span className="skills-more">Type to narrow {matches.length - 24} more.</span>}
          </div>
        </>
      )}

      {picked && (
        <fieldset className="skills-editor">
          <legend>{picked.entry ? `Change ${pickedName}` : `Add ${pickedName}`}</legend>
          <div className="worklog-label" id="skill-level-label">Level</div>
          <div className="skills-levels" role="group" aria-labelledby="skill-level-label">
            {LEVELS.map((name, i) => (
              <button
                key={name}
                type="button"
                className={`skills-level-btn ${level === i + 1 ? 'selected' : ''}`}
                aria-pressed={level === i + 1}
                onClick={() => setLevel(i + 1)}
              >
                <strong>{i + 1}</strong>
                {name}
              </button>
            ))}
          </div>
          <label className="worklog-field">
            <span className="worklog-label">Years of experience</span>
            <input
              className="worklog-title-input skills-years"
              type="number"
              min="0"
              max="80"
              step="0.5"
              value={years}
              onChange={(e) => setYears(e.target.value)}
              aria-label="Years of experience"
            />
          </label>
        </fieldset>
      )}

      {notice && <div className="leave-days-note" role="status">{notice}</div>}
      {error && <div className="worklog-hint error" role="alert">{error}</div>}

      {picked && (
        <div className="issue-create-footer-bar">
          {problem && (
            <span className="issue-create-hint">
              <span className="hint-bullet-dot">•</span>
              {problem}
            </span>
          )}
          <div className="worklog-actions">
            <button type="button" className="skills-btn" onClick={cancel}>Cancel</button>
            <button type="submit" className="worklog-submit" disabled={!!problem || busy}>
              {isLoading ? 'Saving…' : picked.entry ? 'Save changes' : 'Add skill'}
            </button>
          </div>
        </div>
      )}
    </form>
  );
}

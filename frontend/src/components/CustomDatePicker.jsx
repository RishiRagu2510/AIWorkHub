import React, { useState, useEffect, useRef, useMemo } from 'react';

const MONTH_NAMES = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December'
];

const WEEKDAY_NAMES = ['Su', 'Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa'];

function parseIso(isoStr) {
  if (!isoStr || typeof isoStr !== 'string') return null;
  const parts = isoStr.split('-').map(Number);
  if (parts.length < 3 || isNaN(parts[0]) || isNaN(parts[1]) || isNaN(parts[2])) return null;
  return { year: parts[0], month: parts[1] - 1, day: parts[2] };
}

function toIsoString(year, month, day) {
  const y = String(year).padStart(4, '0');
  const m = String(month + 1).padStart(2, '0');
  const d = String(day).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

function formatDateDisplay(isoStr) {
  if (!isoStr) return '';
  const parsed = parseIso(isoStr);
  if (!parsed) return isoStr;
  const date = new Date(parsed.year, parsed.month, parsed.day);
  if (isNaN(date.getTime())) return isoStr;
  return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
}

function getTodayIso() {
  const now = new Date();
  return toIsoString(now.getFullYear(), now.getMonth(), now.getDate());
}

function getCalendarDays(year, month) {
  const firstDayOfWeek = new Date(year, month, 1).getDay(); // 0 = Sunday
  const daysInCurrentMonth = new Date(year, month + 1, 0).getDate();
  const daysInPrevMonth = new Date(year, month, 0).getDate();

  const days = [];
  // Previous month padding
  for (let i = firstDayOfWeek - 1; i >= 0; i--) {
    const d = daysInPrevMonth - i;
    const m = month === 0 ? 11 : month - 1;
    const y = month === 0 ? year - 1 : year;
    days.push({ day: d, month: m, year: y, isCurrentMonth: false });
  }

  // Current month
  for (let d = 1; d <= daysInCurrentMonth; d++) {
    days.push({ day: d, month, year, isCurrentMonth: true });
  }

  // Next month padding to fill either 35 or 42 slots
  const totalSlots = days.length > 35 ? 42 : 35;
  let nextDay = 1;
  while (days.length < totalSlots) {
    const m = month === 11 ? 0 : month + 1;
    const y = month === 11 ? year + 1 : year;
    days.push({ day: nextDay++, month: m, year: y, isCurrentMonth: false });
  }

  return days;
}

export default function CustomDatePicker({
  value,
  defaultValue,
  onChange,
  min,
  max,
  placeholder = 'Select date',
  disabled = false,
  required = false,
  name,
  id,
  className = '',
  'aria-label': ariaLabel,
  ...rest
}) {
  const { type: _type, ...extraInputProps } = rest;
  const [internalValue, setInternalValue] = useState(defaultValue || '');
  const currentValue = value !== undefined ? (value || '') : internalValue;

  const todayIso = useMemo(() => getTodayIso(), []);
  const initialParsed = useMemo(() => parseIso(currentValue) || parseIso(todayIso), [currentValue, todayIso]);

  const [isOpen, setIsOpen] = useState(false);
  const [viewYear, setViewYear] = useState(initialParsed.year);
  const [viewMonth, setViewMonth] = useState(initialParsed.month);

  const containerRef = useRef(null);
  const inputRef = useRef(null);

  // Sync calendar view month/year when a new value is chosen externally
  useEffect(() => {
    if (currentValue) {
      const parsed = parseIso(currentValue);
      if (parsed) {
        setViewYear(parsed.year);
        setViewMonth(parsed.month);
      }
    }
  }, [currentValue]);

  // Click outside listener
  useEffect(() => {
    if (!isOpen) return;

    function handleClickOutside(e) {
      if (containerRef.current && !containerRef.current.contains(e.target)) {
        setIsOpen(false);
      }
    }

    function handleKeyDown(e) {
      if (e.key === 'Escape') {
        setIsOpen(false);
      }
    }

    document.addEventListener('mousedown', handleClickOutside);
    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [isOpen]);

  const notifyChange = (nextIso) => {
    setInternalValue(nextIso);
    if (typeof onChange === 'function') {
      const syntheticEvent = {
        target: { value: nextIso, name, id },
        currentTarget: { value: nextIso, name, id },
        preventDefault: () => {},
        stopPropagation: () => {},
      };
      onChange(syntheticEvent);
    }
  };

  const handleSelectDay = (dayObj) => {
    const nextIso = toIsoString(dayObj.year, dayObj.month, dayObj.day);
    if (min && nextIso < min) return;
    if (max && nextIso > max) return;
    notifyChange(nextIso);
    setIsOpen(false);
  };

  const prevMonth = (e) => {
    e.preventDefault();
    e.stopPropagation();
    if (viewMonth === 0) {
      setViewMonth(11);
      setViewYear((y) => y - 1);
    } else {
      setViewMonth((m) => m - 1);
    }
  };

  const nextMonth = (e) => {
    e.preventDefault();
    e.stopPropagation();
    if (viewMonth === 11) {
      setViewMonth(0);
      setViewYear((y) => y + 1);
    } else {
      setViewMonth((m) => m + 1);
    }
  };

  const selectToday = (e) => {
    e.preventDefault();
    e.stopPropagation();
    if (min && todayIso < min) return;
    if (max && todayIso > max) return;
    const parsed = parseIso(todayIso);
    if (parsed) {
      setViewYear(parsed.year);
      setViewMonth(parsed.month);
    }
    notifyChange(todayIso);
    setIsOpen(false);
  };

  const clearDate = (e) => {
    e.preventDefault();
    e.stopPropagation();
    notifyChange('');
    setIsOpen(false);
  };

  const calendarDays = useMemo(() => getCalendarDays(viewYear, viewMonth), [viewYear, viewMonth]);
  const formattedDisplay = formatDateDisplay(currentValue);

  return (
    <div
      ref={containerRef}
      className={`custom-datepicker-container ${className} ${disabled ? 'is-disabled' : ''} ${isOpen ? 'is-open' : ''}`}
    >
      {/* Hidden native input for form submissions and accessible testing */}
      <input
        ref={inputRef}
        type="date"
        name={name}
        id={id}
        min={min}
        max={max}
        value={currentValue}
        required={required}
        disabled={disabled}
        aria-label={ariaLabel || placeholder || 'Date'}
        className="custom-datepicker-hidden-input"
        tabIndex={-1}
        onChange={(e) => {
          if (e.target.value !== currentValue) {
            notifyChange(e.target.value);
          }
        }}
        {...extraInputProps}
      />

      {/* Styled Interactive Trigger Button */}
      <button
        type="button"
        className={`custom-datepicker-trigger ${isOpen ? 'is-open' : ''} ${currentValue ? 'has-value' : ''}`}
        onClick={(e) => {
          e.preventDefault();
          if (!disabled) setIsOpen((prev) => !prev);
        }}
        disabled={disabled}
        aria-haspopup="dialog"
        aria-expanded={isOpen}
      >
        <span className="custom-datepicker-icon" aria-hidden="true">
          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
            <line x1="16" y1="2" x2="16" y2="6" />
            <line x1="8" y1="2" x2="8" y2="6" />
            <line x1="3" y1="10" x2="21" y2="10" />
          </svg>
        </span>
        <span className={`custom-datepicker-text ${!currentValue ? 'is-placeholder' : ''}`}>
          {formattedDisplay || placeholder}
        </span>
        <span className="custom-datepicker-chevron" aria-hidden="true">
          <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
            <polyline points="6 9 12 15 18 9" />
          </svg>
        </span>
      </button>

      {/* Popover Custom Calendar */}
      {isOpen && (
        <div className="custom-datepicker-popover" role="dialog" aria-modal="true" aria-label="Calendar">
          {/* Calendar Header with Navigation */}
          <div className="cal-header">
            <button
              type="button"
              className="cal-nav-btn prev"
              onClick={prevMonth}
              aria-label="Previous month"
            >
              ‹
            </button>
            <div className="cal-title">
              <span className="cal-month">{MONTH_NAMES[viewMonth]}</span>
              <span className="cal-year">{viewYear}</span>
            </div>
            <button
              type="button"
              className="cal-nav-btn next"
              onClick={nextMonth}
              aria-label="Next month"
            >
              ›
            </button>
          </div>

          {/* Quick Actions Row */}
          <div className="cal-quick-bar">
            <button
              type="button"
              className="cal-quick-today"
              onClick={selectToday}
              disabled={Boolean((min && todayIso < min) || (max && todayIso > max))}
            >
              Today
            </button>
            {currentValue && (
              <button type="button" className="cal-quick-clear" onClick={clearDate}>
                Clear
              </button>
            )}
          </div>

          {/* Weekday Names Header */}
          <div className="cal-weekdays" aria-hidden="true">
            {WEEKDAY_NAMES.map((w) => (
              <span key={w} className="cal-weekday-cell">{w}</span>
            ))}
          </div>

          {/* Days Grid */}
          <div className="cal-days-grid" role="grid">
            {calendarDays.map((dObj, idx) => {
              const cellIso = toIsoString(dObj.year, dObj.month, dObj.day);
              const isSelected = cellIso === currentValue;
              const isToday = cellIso === todayIso;
              const isDisabled = Boolean((min && cellIso < min) || (max && cellIso > max));

              return (
                <button
                  key={`${cellIso}-${idx}`}
                  type="button"
                  role="gridcell"
                  aria-selected={isSelected}
                  disabled={isDisabled}
                  className={`cal-day-cell ${dObj.isCurrentMonth ? 'in-month' : 'out-month'} ${isSelected ? 'is-selected' : ''} ${isToday ? 'is-today' : ''}`}
                  onClick={() => handleSelectDay(dObj)}
                >
                  <span className="cal-day-num">{dObj.day}</span>
                </button>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}

import React from 'react';

interface HeaderBarProps {
  onExport: () => void;
}

export const HeaderBar: React.FC<HeaderBarProps> = ({ onExport }) => {
  return (
    <header
      style={{
        height: '48px',
        backgroundColor: '#1e1f29',
        color: '#ffffff',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: '0 16px',
        zIndex: 10,
        boxShadow: '0 2px 4px rgba(0,0,0,0.15)',
      }}
    >
      <span style={{ fontWeight: 600, fontSize: '15px' }}>Process Optimizer Flowchart</span>
      <button
        onClick={onExport}
        style={{
          backgroundColor: '#aa3bff',
          color: '#fff',
          border: 'none',
          borderRadius: '4px',
          padding: '6px 14px',
          fontSize: '13px',
          fontWeight: 600,
          cursor: 'pointer',
        }}
      >
        Save &amp; Export JSON
      </button>
    </header>
  );
};

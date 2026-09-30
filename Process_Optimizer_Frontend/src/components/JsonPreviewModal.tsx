import React from 'react';

interface JsonPreviewModalProps {
  json: string;
  onClose: () => void;
}

export const JsonPreviewModal: React.FC<JsonPreviewModalProps> = ({ json, onClose }) => {
  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        backgroundColor: 'rgba(0,0,0,0.6)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        zIndex: 1000,
      }}
    >
      <div
        style={{
          backgroundColor: '#fff',
          color: '#333',
          width: '720px',
          maxWidth: '90vw',
          maxHeight: '80vh',
          borderRadius: '8px',
          display: 'flex',
          flexDirection: 'column',
          padding: '20px',
          boxShadow: '0 8px 24px rgba(0,0,0,0.25)',
        }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px' }}>
          <h3 style={{ margin: 0, fontSize: '18px' }}>Exported Backend Payload</h3>
          <button
            onClick={onClose}
            style={{
              background: 'none',
              border: 'none',
              fontSize: '18px',
              cursor: 'pointer',
              fontWeight: 'bold',
            }}
          >
            ✕
          </button>
        </div>
        <pre
          style={{
            flex: 1,
            overflow: 'auto',
            backgroundColor: '#1e1f29',
            color: '#8be9fd',
            padding: '12px',
            borderRadius: '6px',
            fontSize: '12px',
            lineHeight: '1.4',
            margin: 0,
          }}
        >
          {json}
        </pre>
        <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '12px', gap: '8px' }}>
          <button
            onClick={() => {
              navigator.clipboard.writeText(json);
              alert('Copied JSON payload to clipboard!');
            }}
            style={{
              padding: '6px 12px',
              backgroundColor: '#e5e7eb',
              border: '1px solid #d1d5db',
              borderRadius: '4px',
              cursor: 'pointer',
              fontWeight: 500,
            }}
          >
            Copy JSON
          </button>
          <button
            onClick={onClose}
            style={{
              padding: '6px 12px',
              backgroundColor: '#aa3bff',
              color: '#fff',
              border: 'none',
              borderRadius: '4px',
              cursor: 'pointer',
              fontWeight: 600,
            }}
          >
            Close
          </button>
        </div>
      </div>
    </div>
  );
};

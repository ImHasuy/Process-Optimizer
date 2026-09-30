import { useState, useRef } from 'react';
import { HeaderBar } from './components/HeaderBar';
import { BpmnCanvas } from './components/BpmnCanvas';
import { JsonPreviewModal } from './components/JsonPreviewModal';
import { ProcessExporter } from './services/ProcessExporter';

// Styles
import 'bpmn-js/dist/assets/diagram-js.css';
import 'bpmn-js/dist/assets/bpmn-font/css/bpmn.css';
import '@bpmn-io/properties-panel/assets/properties-panel.css';
import './App.css';

function App() {
  const modelerRef = useRef<any>(null);
  const [jsonPreview, setJsonPreview] = useState<string | null>(null);

  const handleExport = async () => {
    if (!modelerRef.current) return;
    try {
      const exporter = new ProcessExporter(modelerRef.current);
      const data = await exporter.export();
      console.log('Exported Process Data for Backend:', data);
      setJsonPreview(JSON.stringify(data, null, 2));
    } catch (err) {
      console.error('Failed to export process data:', err);
      alert('Failed to export diagram data.');
    }
  };

  return (
    <div
      style={{
        position: 'fixed',
        top: 0,
        left: 0,
        width: '100vw',
        height: '100vh',
        display: 'flex',
        flexDirection: 'column',
        margin: 0,
        padding: 0,
        overflow: 'hidden',
      }}
    >
      <HeaderBar onExport={handleExport} />
      <BpmnCanvas onModelerReady={(modeler) => (modelerRef.current = modeler)} />
      {jsonPreview && <JsonPreviewModal json={jsonPreview} onClose={() => setJsonPreview(null)} />}
    </div>
  );
}

export default App;
import React, { useEffect, useRef } from 'react';
import BpmnModeler from 'bpmn-js/lib/Modeler';
import {
  BpmnPropertiesPanelModule,
  BpmnPropertiesProviderModule,
} from 'bpmn-js-properties-panel';
import { customPropertiesProviderModule } from '../properties-panel';
import { customModdle } from '../models/customModdle';
import { initialDiagramXml } from '../constants/initialDiagramXml';

interface BpmnCanvasProps {
  onModelerReady: (modeler: any) => void;
}

export const BpmnCanvas: React.FC<BpmnCanvasProps> = ({ onModelerReady }) => {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const propertiesPanelRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    if (!containerRef.current || !propertiesPanelRef.current) return;

    // Instantiate Modeler with properties panel and custom provider
    const modeler = new BpmnModeler({
      container: containerRef.current,
      propertiesPanel: {
        parent: propertiesPanelRef.current,
      },
      additionalModules: [
        BpmnPropertiesPanelModule,
        BpmnPropertiesProviderModule,
        customPropertiesProviderModule,
      ],
      moddleExtensions: {
        optimizer: customModdle,
      },
      keyboard: {
        bindTo: document,
      },
    });

    // Import starter diagram
    modeler
      .importXML(initialDiagramXml)
      .then(() => {
        const canvas = modeler.get('canvas') as any;
        canvas.zoom('fit-viewport');
        onModelerReady(modeler);
      })
      .catch((err: any) => {
        console.error('Error importing BPMN XML:', err);
      });

    return () => {
      modeler.destroy();
      onModelerReady(null);
    };
  }, []);

  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'row', overflow: 'hidden' }}>
      {/* BPMN Canvas Container */}
      <div
        ref={containerRef}
        style={{
          flex: 1,
          height: '100%',
          backgroundColor: '#fff',
        }}
      />

      {/* Properties Panel Container */}
      <div
        ref={propertiesPanelRef}
        style={{
          width: '320px',
          height: '100%',
          borderLeft: '1px solid #d0d0d0',
          backgroundColor: '#f8f8f8',
          overflowY: 'auto',
          boxSizing: 'border-box',
        }}
      />
    </div>
  );
};

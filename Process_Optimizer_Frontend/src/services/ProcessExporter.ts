import type { ProcessExportData, StateNode, Branch, BranchType } from '../models/ProcessTypes';

export class ProcessExporter {
  private modeler: any;

  constructor(modeler: any) {
    this.modeler = modeler;
  }

  /**
   * Determines the logical branch type (xor, nor, and, or, sequence).
   */
  public getBranchType(flow: any): BranchType {
    const source = flow.source;
    const bo = flow.businessObject;

    // 1. Explicitly configured branchType attribute
    if (bo?.branchType || bo?.$attrs?.branchType || bo?.$attrs?.['optimizer:branchType']) {
      return (bo.branchType || bo.$attrs.branchType || bo.$attrs['optimizer:branchType']).toLowerCase();
    }

    // 2. Flow name or label overrides
    const name = (bo?.name || '').trim().toLowerCase();
    if (name === 'xor' || name.startsWith('xor') || name.includes('[xor]')) return 'xor';
    if (name === 'nor' || name.startsWith('nor') || name.includes('[nor]')) return 'nor';
    if (name === 'and' || name.startsWith('and') || name.includes('[and]')) return 'and';
    if (name === 'or' || name.startsWith('or') || name.includes('[or]')) return 'or';

    // 3. Infer from source gateway type
    if (source) {
      const sourceType = source.type;
      const isDefaultFlow = source.businessObject?.default?.id === flow.id;

      if (sourceType === 'bpmn:ExclusiveGateway') {
        // In BPMN, exclusive gateway forks XOR paths, while default/fallback is NOR (when no other condition is met)
        return isDefaultFlow ? 'nor' : 'xor';
      }

      if (sourceType === 'bpmn:InclusiveGateway') {
        return isDefaultFlow ? 'nor' : 'or';
      }

      if (sourceType === 'bpmn:ParallelGateway') {
        return 'and';
      }

      if (sourceType === 'bpmn:ComplexGateway') {
        return 'complex';
      }
    }

    // 4. Activity with multiple outgoing flows (implicit decision)
    if (source && source.outgoing && source.outgoing.length > 1) {
      const isDefaultFlow = source.businessObject?.default?.id === flow.id;
      return isDefaultFlow ? 'nor' : 'xor';
    }

    return 'sequence';
  }

  /**
   * Extracts and structures complete flowchart data with Depends_on, Connects_to, and typed branches.
   */
  public async export(): Promise<ProcessExportData> {
    const elementRegistry = this.modeler.get('elementRegistry');

    // Extract all states (activities, events, gateways)
    const states: StateNode[] = elementRegistry
      .filter((element: any) => {
        return (
          element.type !== 'label' &&
          element.type !== 'bpmn:SequenceFlow' &&
          element.type !== 'bpmn:Process' &&
          element.type !== 'bpmn:Collaboration'
        );
      })
      .map((element: any) => {
        const bo = element.businessObject;

        const time =
          (typeof bo.get === 'function' ? bo.get('time') : bo.time) ||
          bo.$attrs?.time ||
          bo.$attrs?.['optimizer:time'] ||
          '';

        const price =
          (typeof bo.get === 'function' ? bo.get('price') : bo.price) ||
          bo.$attrs?.price ||
          bo.$attrs?.['optimizer:price'] ||
          '';

        // Depends_on: nodes that connect INTO this element (A -> B means B depends on A)
        const Depends_on = (element.incoming || [])
          .map((flow: any) => flow.source?.id)
          .filter(Boolean);

        // Connects_to: nodes that START from this element (A -> B means A connects to B)
        const Connects_to = (element.outgoing || [])
          .map((flow: any) => flow.target?.id)
          .filter(Boolean);

        return {
          id: element.id,
          name: bo.name || '',
          type: element.type,
          time,
          price,
          Depends_on,
          Connects_to,
        };
      });

    // Extract all branches with their branch types (xor, nor, and, or, sequence)
    const branches: Branch[] = elementRegistry
      .filter((element: any) => element.type === 'bpmn:SequenceFlow')
      .map((flow: any) => ({
        id: flow.id,
        name: flow.businessObject?.name || '',
        source: flow.source?.id || null,
        target: flow.target?.id || null,
        type: this.getBranchType(flow),
        condition: flow.businessObject?.conditionExpression?.body || null,
      }));

    return {
      states,
      branches,
    };
  }
}

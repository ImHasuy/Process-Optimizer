import { useService } from 'bpmn-js-properties-panel';
import { SelectEntry } from '@bpmn-io/properties-panel';

export function BranchTypeEntry(props: any) {
  const { element, id } = props;
  const modeling = useService('modeling');
  const translate = useService('translate');

  const getValue = () => {
    const bo = element.businessObject;
    const explicit =
      (typeof bo.get === 'function' ? bo.get('branchType') : bo.branchType) ||
      bo.$attrs?.branchType ||
      bo.$attrs?.['optimizer:branchType'];

    if (explicit) return explicit;

    // Infer fallback from source gateway
    const source = element.source;
    if (source) {
      if (source.type === 'bpmn:ExclusiveGateway') {
        const isDefault = source.businessObject?.default?.id === element.id;
        return isDefault ? 'nor' : 'xor';
      }
      if (source.type === 'bpmn:ParallelGateway') return 'and';
      if (source.type === 'bpmn:InclusiveGateway') {
        const isDefault = source.businessObject?.default?.id === element.id;
        return isDefault ? 'nor' : 'or';
      }
    }

    return 'sequence';
  };

  const setValue = (value: string) => {
    try {
      modeling.updateModdleProperties(element, element.businessObject, {
        branchType: value,
      });
    } catch {
      modeling.updateProperties(element, {
        branchType: value,
      });
    }
    if (!element.businessObject.$attrs) {
      element.businessObject.$attrs = {};
    }
    element.businessObject.$attrs['optimizer:branchType'] = value;
    element.businessObject.branchType = value;
  };

  const getOptions = () => [
    { value: 'sequence', label: 'Standard Sequence' },
    { value: 'xor', label: 'XOR (Exclusive)' },
    { value: 'nor', label: 'NOR (Default / Fallback)' },
    { value: 'and', label: 'AND (Parallel)' },
    { value: 'or', label: 'OR (Inclusive)' },
  ];

  return SelectEntry({
    element,
    id,
    label: translate ? translate('Branch Type') : 'Branch Type',
    description: 'Logical branching behavior (e.g. XOR, NOR)',
    getValue,
    setValue,
    getOptions,
  });
}

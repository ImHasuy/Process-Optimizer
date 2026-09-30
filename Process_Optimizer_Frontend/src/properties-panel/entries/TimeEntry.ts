import { useService } from 'bpmn-js-properties-panel';
import { TextFieldEntry } from '@bpmn-io/properties-panel';

export function TimeEntry(props: any) {
  const { element, id } = props;
  const modeling = useService('modeling');
  const debounce = useService('debounceInput');
  const translate = useService('translate');

  const getValue = () => {
    const bo = element.businessObject;
    return (
      (typeof bo.get === 'function' ? bo.get('time') : bo.time) ||
      bo.$attrs?.time ||
      bo.$attrs?.['optimizer:time'] ||
      ''
    );
  };

  const setValue = (value: string) => {
    try {
      modeling.updateModdleProperties(element, element.businessObject, {
        time: value || undefined,
      });
    } catch {
      modeling.updateProperties(element, {
        time: value || undefined,
      });
    }
    if (!element.businessObject.$attrs) {
      element.businessObject.$attrs = {};
    }
    element.businessObject.$attrs['optimizer:time'] = value;
    element.businessObject.time = value;
  };

  return TextFieldEntry({
    element,
    id,
    label: translate ? translate('Time') : 'Time',
    description: 'Execution duration (e.g. 15m, 2h)',
    getValue,
    setValue,
    debounce,
  });
}

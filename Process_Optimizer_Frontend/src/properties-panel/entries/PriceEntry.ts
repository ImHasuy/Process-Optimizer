import { useService } from 'bpmn-js-properties-panel';
import { TextFieldEntry } from '@bpmn-io/properties-panel';

export function PriceEntry(props: any) {
  const { element, id } = props;
  const modeling = useService('modeling');
  const debounce = useService('debounceInput');
  const translate = useService('translate');

  const getValue = () => {
    const bo = element.businessObject;
    return (
      (typeof bo.get === 'function' ? bo.get('price') : bo.price) ||
      bo.$attrs?.price ||
      bo.$attrs?.['optimizer:price'] ||
      ''
    );
  };

  const setValue = (value: string) => {
    try {
      modeling.updateModdleProperties(element, element.businessObject, {
        price: value || undefined,
      });
    } catch {
      modeling.updateProperties(element, {
        price: value || undefined,
      });
    }
    if (!element.businessObject.$attrs) {
      element.businessObject.$attrs = {};
    }
    element.businessObject.$attrs['optimizer:price'] = value;
    element.businessObject.price = value;
  };

  return TextFieldEntry({
    element,
    id,
    label: translate ? translate('Price') : 'Price',
    description: 'Execution cost (e.g. $50, 100 EUR)',
    getValue,
    setValue,
    debounce,
  });
}

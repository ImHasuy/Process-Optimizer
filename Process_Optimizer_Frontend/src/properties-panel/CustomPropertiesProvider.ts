import { isTextFieldEntryEdited, isSelectEntryEdited } from '@bpmn-io/properties-panel';
import { TimeEntry } from './entries/TimeEntry';
import { PriceEntry } from './entries/PriceEntry';
import { BranchTypeEntry } from './entries/BranchTypeEntry';

export class CustomPropertiesProvider {
  static $inject = ['propertiesPanel'];

  constructor(propertiesPanel: any) {
    propertiesPanel.registerProvider(500, this);
  }

  getGroups(element: any) {
    return (_groups: any[]) => {
      // If a SequenceFlow (branch) is selected, show Branch Properties (type: xor, nor, etc.)
      if (element.type === 'bpmn:SequenceFlow') {
        return [
          {
            id: 'optimizer-branch-properties',
            label: 'Branch Properties',
            entries: [
              {
                id: 'branchType',
                component: BranchTypeEntry,
                isEdited: isSelectEntryEdited,
              },
            ],
          },
        ];
      }

      // For states / flow nodes, show Time and Price
      return [
        {
          id: 'optimizer-properties',
          label: 'Properties',
          entries: [
            {
              id: 'time',
              component: TimeEntry,
              isEdited: isTextFieldEntryEdited,
            },
            {
              id: 'price',
              component: PriceEntry,
              isEdited: isTextFieldEntryEdited,
            },
          ],
        },
      ];
    };
  }
}

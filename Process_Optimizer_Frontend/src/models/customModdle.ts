// Custom Moddle extension defining 'time', 'price', and 'branchType' as attributes on BPMN elements
export const customModdle = {
  name: 'ProcessOptimizer',
  prefix: 'optimizer',
  uri: 'http://process-optimizer.com/schema/1.0/bpmn',
  xml: {
    tagAlias: 'lowerCase',
  },
  types: [
    {
      name: 'OptimizedElement',
      isAbstract: true,
      extends: ['bpmn:BaseElement'],
      properties: [
        {
          name: 'time',
          isAttr: true,
          type: 'String',
        },
        {
          name: 'price',
          isAttr: true,
          type: 'String',
        },
        {
          name: 'branchType',
          isAttr: true,
          type: 'String',
        },
      ],
    },
  ],
};

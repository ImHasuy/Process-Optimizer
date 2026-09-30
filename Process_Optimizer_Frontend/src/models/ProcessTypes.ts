export interface StateNode {
  id: string;
  name: string;
  type: string;
  time: string;
  price: string;
  Depends_on: string[];
  Connects_to: string[];
}

export type BranchType = 'sequence' | 'xor' | 'nor' | 'and' | 'or' | 'complex' | string;

export interface Branch {
  id: string;
  name: string;
  source: string | null;
  target: string | null;
  type: BranchType;
  condition: string | null;
}

export interface ProcessExportData {
  states: StateNode[];
  branches: Branch[];
}

// Runtime export ensures bundlers do not treat the file as empty
export const ProcessTypesVersion = '1.0.0';

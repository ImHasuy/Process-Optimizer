namespace ProjectOptimizerAPI.Lib.Database
{
	public static class SeedDiagrams
	{
		public const string WorkedExampleXml = """
<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI" xmlns:dc="http://www.omg.org/spec/DD/20100524/DC" xmlns:di="http://www.omg.org/spec/DD/20100524/DI" xmlns:po="http://projectoptimizer.app/schema/bpmn/1.0" id="Definitions_1" targetNamespace="http://bpmn.io/schema/bpmn">
  <bpmn:process id="Process_1" isExecutable="false">
    <bpmn:startEvent id="S" name="Kick-off">
      <bpmn:outgoing>F1</bpmn:outgoing>
    </bpmn:startEvent>
    <bpmn:task id="A" name="Design" po:duration="5" po:cost="4000">
      <bpmn:incoming>F1</bpmn:incoming>
      <bpmn:outgoing>F2</bpmn:outgoing>
    </bpmn:task>
    <bpmn:parallelGateway id="G1">
      <bpmn:incoming>F2</bpmn:incoming>
      <bpmn:outgoing>F3</bpmn:outgoing>
      <bpmn:outgoing>F4</bpmn:outgoing>
    </bpmn:parallelGateway>
    <bpmn:task id="B" name="Backend" po:duration="10" po:cost="8000">
      <bpmn:incoming>F3</bpmn:incoming>
      <bpmn:outgoing>F5</bpmn:outgoing>
    </bpmn:task>
    <bpmn:task id="C" name="Frontend" po:duration="8" po:cost="6000">
      <bpmn:incoming>F4</bpmn:incoming>
      <bpmn:outgoing>F6</bpmn:outgoing>
    </bpmn:task>
    <bpmn:parallelGateway id="G2">
      <bpmn:incoming>F5</bpmn:incoming>
      <bpmn:incoming>F6</bpmn:incoming>
      <bpmn:outgoing>F7</bpmn:outgoing>
    </bpmn:parallelGateway>
    <bpmn:task id="D" name="Test" po:duration="4" po:cost="2000" po:parallelizable="false">
      <bpmn:incoming>F7</bpmn:incoming>
      <bpmn:outgoing>F8</bpmn:outgoing>
    </bpmn:task>
    <bpmn:exclusiveGateway id="G3" name="Where to deploy?">
      <bpmn:incoming>F8</bpmn:incoming>
      <bpmn:outgoing>F9</bpmn:outgoing>
      <bpmn:outgoing>F10</bpmn:outgoing>
    </bpmn:exclusiveGateway>
    <bpmn:task id="E" name="Deploy on-prem" po:duration="2" po:cost="3000">
      <bpmn:incoming>F9</bpmn:incoming>
      <bpmn:outgoing>F11</bpmn:outgoing>
    </bpmn:task>
    <bpmn:task id="F" name="Deploy to cloud" po:duration="1" po:cost="5000">
      <bpmn:incoming>F10</bpmn:incoming>
      <bpmn:outgoing>F12</bpmn:outgoing>
    </bpmn:task>
    <bpmn:exclusiveGateway id="G4">
      <bpmn:incoming>F11</bpmn:incoming>
      <bpmn:incoming>F12</bpmn:incoming>
      <bpmn:outgoing>F13</bpmn:outgoing>
    </bpmn:exclusiveGateway>
    <bpmn:endEvent id="End" name="Done">
      <bpmn:incoming>F13</bpmn:incoming>
    </bpmn:endEvent>
    <bpmn:sequenceFlow id="F1" sourceRef="S" targetRef="A" />
    <bpmn:sequenceFlow id="F2" sourceRef="A" targetRef="G1" />
    <bpmn:sequenceFlow id="F3" sourceRef="G1" targetRef="B" />
    <bpmn:sequenceFlow id="F4" sourceRef="G1" targetRef="C" />
    <bpmn:sequenceFlow id="F5" sourceRef="B" targetRef="G2" />
    <bpmn:sequenceFlow id="F6" sourceRef="C" targetRef="G2" />
    <bpmn:sequenceFlow id="F7" sourceRef="G2" targetRef="D" />
    <bpmn:sequenceFlow id="F8" sourceRef="D" targetRef="G3" />
    <bpmn:sequenceFlow id="F9" sourceRef="G3" targetRef="E" />
    <bpmn:sequenceFlow id="F10" sourceRef="G3" targetRef="F" />
    <bpmn:sequenceFlow id="F11" sourceRef="E" targetRef="G4" />
    <bpmn:sequenceFlow id="F12" sourceRef="F" targetRef="G4" />
    <bpmn:sequenceFlow id="F13" sourceRef="G4" targetRef="End" />
  </bpmn:process>
  <bpmndi:BPMNDiagram id="BPMNDiagram_1">
    <bpmndi:BPMNPlane id="BPMNPlane_1" bpmnElement="Process_1">
      <bpmndi:BPMNShape id="S_di" bpmnElement="S">
        <dc:Bounds x="152" y="182" width="36" height="36" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id="A_di" bpmnElement="A">
        <dc:Bounds x="240" y="160" width="100" height="80" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id="G1_di" bpmnElement="G1">
        <dc:Bounds x="395" y="175" width="50" height="50" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id="B_di" bpmnElement="B">
        <dc:Bounds x="500" y="80" width="100" height="80" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id="C_di" bpmnElement="C">
        <dc:Bounds x="500" y="240" width="100" height="80" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id="G2_di" bpmnElement="G2">
        <dc:Bounds x="655" y="175" width="50" height="50" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id="D_di" bpmnElement="D">
        <dc:Bounds x="760" y="160" width="100" height="80" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id="G3_di" bpmnElement="G3" isMarkerVisible="true">
        <dc:Bounds x="915" y="175" width="50" height="50" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id="E_di" bpmnElement="E">
        <dc:Bounds x="1020" y="80" width="100" height="80" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id="F_di" bpmnElement="F">
        <dc:Bounds x="1020" y="240" width="100" height="80" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id="G4_di" bpmnElement="G4" isMarkerVisible="true">
        <dc:Bounds x="1175" y="175" width="50" height="50" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id="End_di" bpmnElement="End">
        <dc:Bounds x="1282" y="182" width="36" height="36" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNEdge id="F1_di" bpmnElement="F1">
        <di:waypoint x="188" y="200" />
        <di:waypoint x="240" y="200" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id="F2_di" bpmnElement="F2">
        <di:waypoint x="340" y="200" />
        <di:waypoint x="395" y="200" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id="F3_di" bpmnElement="F3">
        <di:waypoint x="420" y="175" />
        <di:waypoint x="420" y="120" />
        <di:waypoint x="500" y="120" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id="F4_di" bpmnElement="F4">
        <di:waypoint x="420" y="225" />
        <di:waypoint x="420" y="280" />
        <di:waypoint x="500" y="280" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id="F5_di" bpmnElement="F5">
        <di:waypoint x="600" y="120" />
        <di:waypoint x="680" y="120" />
        <di:waypoint x="680" y="175" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id="F6_di" bpmnElement="F6">
        <di:waypoint x="600" y="280" />
        <di:waypoint x="680" y="280" />
        <di:waypoint x="680" y="225" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id="F7_di" bpmnElement="F7">
        <di:waypoint x="705" y="200" />
        <di:waypoint x="760" y="200" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id="F8_di" bpmnElement="F8">
        <di:waypoint x="860" y="200" />
        <di:waypoint x="915" y="200" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id="F9_di" bpmnElement="F9">
        <di:waypoint x="940" y="175" />
        <di:waypoint x="940" y="120" />
        <di:waypoint x="1020" y="120" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id="F10_di" bpmnElement="F10">
        <di:waypoint x="940" y="225" />
        <di:waypoint x="940" y="280" />
        <di:waypoint x="1020" y="280" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id="F11_di" bpmnElement="F11">
        <di:waypoint x="1120" y="120" />
        <di:waypoint x="1200" y="120" />
        <di:waypoint x="1200" y="175" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id="F12_di" bpmnElement="F12">
        <di:waypoint x="1120" y="280" />
        <di:waypoint x="1200" y="280" />
        <di:waypoint x="1200" y="225" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id="F13_di" bpmnElement="F13">
        <di:waypoint x="1225" y="200" />
        <di:waypoint x="1282" y="200" />
      </bpmndi:BPMNEdge>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn:definitions>
""";
	}
}

"""
Visio to BPMN 2.0 Converter
Converts .vsdx files to BPMN 2.0 XML format using the vsdx library

Requirements:
    pip install vsdx

Usage:
    python convert_visio_to_bpmn.py
"""

import os
import re
import json
import uuid
import zipfile
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Dict, List, Tuple, Optional
from dataclasses import dataclass, field

try:
    from vsdx import VisioFile
except ImportError:
    print("Please install vsdx library: pip install vsdx")
    exit(1)


@dataclass
class BPMNShape:
    """Represents a BPMN shape extracted from Visio"""
    id: str
    name: str
    shape_type: str  # task, startEvent, endEvent, exclusiveGateway, parallelGateway, etc.
    x: float
    y: float
    width: float
    height: float
    lane_id: Optional[str] = None


@dataclass
class BPMNConnection:
    """Represents a sequence flow between shapes"""
    id: str
    source_id: str
    target_id: str
    name: str = ""
    waypoints: List[Tuple[float, float]] = field(default_factory=list)


@dataclass
class BPMNLane:
    """Represents a swimlane/pool"""
    id: str
    name: str
    x: float
    y: float
    width: float
    height: float
    is_pool: bool = False
    is_horizontal: bool = True
    flow_node_refs: List[str] = field(default_factory=list)


class VisioToBPMNConverter:
    """Converts Visio diagrams to BPMN 2.0 XML"""

    PX_PER_INCH = 96.0
    
    # Mapping of Visio shape names to BPMN element types
    SHAPE_MAPPING = {
        # Start events
        'start': 'startEvent',
        'start event': 'startEvent',
        'بداية': 'startEvent',
        'البداية': 'startEvent',
        
        # End events
        'end': 'endEvent',
        'end event': 'endEvent', 
        'نهاية': 'endEvent',
        'النهاية': 'endEvent',
        'terminator': 'endEvent',
        
        # Tasks
        'process': 'task',
        'task': 'task',
        'activity': 'task',
        'action': 'task',
        'عملية': 'task',
        'مهمة': 'task',
        'إجراء': 'task',
        
        # Gateways
        'decision': 'exclusiveGateway',
        'exclusive gateway': 'exclusiveGateway',
        'gateway': 'exclusiveGateway',
        'قرار': 'exclusiveGateway',
        'parallel': 'parallelGateway',
        'parallel gateway': 'parallelGateway',
        
        # Subprocesses
        'subprocess': 'subProcess',
        'sub-process': 'subProcess',
        
        # Documents/Data
        'document': 'dataObject',
        'data': 'dataObject',
        'مستند': 'dataObject',
    }
    
    def __init__(self, visio_path: str):
        self.visio_path = visio_path
        self.shapes: List[BPMNShape] = []
        self.connections: List[BPMNConnection] = []
        self.lanes: List[BPMNLane] = []
        self.pool: Optional[BPMNLane] = None
        self.process_name = Path(visio_path).stem

        # Visio page dimensions (in pixels) used for DI Y-axis flipping.
        # Populated from the VSDX page XML when available.
        self.page_width_px: Optional[float] = None
        self.page_height_px: Optional[float] = None
        
    def extract_from_visio(self) -> bool:
        """Extract shapes and connections from Visio file"""
        try:
            with VisioFile(self.visio_path) as vf:
                # Process each page
                for idx, page in enumerate(vf.pages, start=1):
                    self._extract_page(page, page_number=idx)
            return True
        except Exception as e:
            print(f"Error extracting from {self.visio_path}: {e}")
            return False
    
    def _extract_page(self, page, page_number: int = 1):
        """Extract shapes from a Visio page"""
        shape_id_map = {}  # Map Visio IDs to BPMN IDs

        # Build a map of all Visio shapes (including connectors) for this page.
        visio_shapes_by_id = {s.ID: s for s in page.all_shapes}

        # Load page dimensions from XML (once per file; most VSDX here have 1 page)
        self._try_load_page_dimensions_from_xml(page_number=page_number)

        # Extract pool + lanes from page XML (vSDX library doesn't expose container names)
        self._extract_pool_and_lanes_from_page_xml(
            page_number=page_number,
            visio_shapes_by_id=visio_shapes_by_id
        )
        
        for shape in page.all_shapes:
            bpmn_shape = self._convert_shape(shape)
            if bpmn_shape:
                self.shapes.append(bpmn_shape)
                shape_id_map[shape.ID] = bpmn_shape.id
                
        # Assign shapes to lanes (based on geometry overlap)
        self._assign_shapes_to_lanes()

        # Extract connections from the authoritative Visio <Connects> table (in the page XML)
        self.connections.extend(
            self._extract_connections_from_page_xml(
                page_number=page_number,
                bpmn_id_map=shape_id_map,
                visio_shapes_by_id=visio_shapes_by_id
            )
        )
    
    def _convert_shape(self, shape) -> Optional[BPMNShape]:
        """Convert a Visio shape to BPMN shape"""
        try:
            # Get shape name/text
            name = ""
            if hasattr(shape, 'text') and shape.text:
                name = shape.text.strip()
            elif hasattr(shape, 'name') and shape.name:
                name = shape.name
                
            # Skip connectors and empty shapes
            if self._is_connector(shape) or not name:
                return None
            
            # Determine BPMN type
            shape_type = self._determine_bpmn_type(shape, name)
            if not shape_type:
                shape_type = 'task'  # Default to task
            
            # Get position and size
            x, y = self._get_position(shape)
            width, height = self._get_size(shape)
            
            return BPMNShape(
                id=f"Shape_{shape.ID}",
                name=name,
                shape_type=shape_type,
                x=x,
                y=y,
                width=width,
                height=height
            )
        except Exception as e:
            return None

    def _determine_bpmn_type(self, shape, text: str) -> str:
        """Determine BPMN element type from Visio shape"""
        master_name = (getattr(shape, 'master_shape_name', None) or "").lower()
        t = (text or "").strip().lower()

        # Strong precedence for Arabic start/end keywords (avoid accidental 'start' substring matches)
        if t in ("نهاية", "النهاية"):
            return "endEvent"
        if t in ("بداية", "البداية"):
            return "startEvent"

        # Prefer end if both appear in master/text like "Start/End"
        if "end" in master_name or "end" in t or "terminator" in master_name:
            return "endEvent"
        if "start" in master_name or "start" in t:
            return "startEvent"

        # Match remaining types (longer keys first)
        for key, bpmn_type in sorted(self.SHAPE_MAPPING.items(), key=lambda kv: len(kv[0]), reverse=True):
            if key in master_name or key in t:
                return bpmn_type

        return "task"

    def _is_connector(self, shape) -> bool:
        """Check if shape is a connector/line"""
        cells = getattr(shape, 'cells', None)
        if hasattr(cells, 'get') and (cells.get('BeginX') is not None or cells.get('EndX') is not None):
            return True

        name = (getattr(shape, 'master_shape_name', None) or "").lower()
        return any(x in name for x in ['connector', 'line', 'arrow', 'dynamic'])

    def _is_lane_shape(self, shape) -> bool:
        """Legacy lane detection (kept for compatibility)."""
        if hasattr(shape, 'master_shape_name') and shape.master_shape_name:
            name = shape.master_shape_name.lower()
            return any(x in name for x in ['lane', 'pool', 'container', 'swimlane', 'cff'])
        return False

    def _extract_pool_and_lanes_from_page_xml(self, page_number: int, visio_shapes_by_id: Dict[str, object]) -> None:
        """Extract pool + swimlanes from the Visio page XML.

        The vsdx python library doesn't expose NameU/Master info for container shapes in these files,
        but the VSDX XML does (e.g., NameU="CFF Container", "Swimlane (vertical)").
        """
        try:
            with zipfile.ZipFile(self.visio_path, 'r') as zf:
                page_xml_path = f"visio/pages/page{page_number}.xml"
                if page_xml_path not in zf.namelist():
                    return

                xml_text = zf.read(page_xml_path).decode('utf-8', errors='ignore')
                ns = {'v': 'http://schemas.microsoft.com/office/visio/2012/main'}
                root = ET.fromstring(xml_text)

                def visio_shape_bounds_px(visio_id: str) -> Optional[Tuple[float, float, float, float]]:
                    s = visio_shapes_by_id.get(visio_id)
                    if s is None or not hasattr(s, 'cells'):
                        return None
                    try:
                        pinx = float(getattr(s.cells.get('PinX'), 'value', 0.0) or 0.0) * self.PX_PER_INCH
                        piny = float(getattr(s.cells.get('PinY'), 'value', 0.0) or 0.0) * self.PX_PER_INCH
                        w = float(getattr(s.cells.get('Width'), 'value', 0.0) or 0.0) * self.PX_PER_INCH
                        h = float(getattr(s.cells.get('Height'), 'value', 0.0) or 0.0) * self.PX_PER_INCH
                        return (pinx, piny, max(w, 10.0), max(h, 10.0))
                    except:
                        return None

                def extract_text(elem: ET.Element) -> str:
                    # Visio text can be nested; itertext collects it all.
                    t = ''.join(elem.itertext())
                    return (t or '').strip()

                # Collect lanes/pool at top-level shapes
                lanes: List[BPMNLane] = []
                pool: Optional[BPMNLane] = None

                for s in root.findall('.//v:Shapes/v:Shape', ns):
                    sid = s.attrib.get('ID')
                    if not sid:
                        continue
                    nameu = (s.attrib.get('NameU') or '').strip()
                    nameu_l = nameu.lower()

                    # Pool (container)
                    if nameu_l == 'cff container':
                        b = visio_shape_bounds_px(sid)
                        if b:
                            pinx, piny, w, h = b
                            # Prefer embedded text if any, else file name
                            pool_name = extract_text(s) or self.process_name
                            pool = BPMNLane(
                                id='Participant_1',
                                name=pool_name,
                                x=pinx,
                                y=piny,
                                width=w,
                                height=h,
                                is_pool=True,
                                is_horizontal=True
                            )
                        continue

                    # Lanes (swimlanes)
                    if 'swimlane' in nameu_l and 'list' not in nameu_l:
                        b = visio_shape_bounds_px(sid)
                        if not b:
                            continue
                        pinx, piny, w, h = b

                        lane_name = ''
                        # Try to find a text label inside the group first
                        lane_name = extract_text(s)
                        if not lane_name:
                            # Remove Visio suffixes like ".21" for readability
                            lane_name = re.sub(r'\.[0-9]+$', '', nameu).strip() or nameu

                        is_horizontal = ('vertical' not in nameu_l)

                        lanes.append(BPMNLane(
                            id=f"Lane_{sid}",
                            name=lane_name,
                            x=pinx,
                            y=piny,
                            width=w,
                            height=h,
                            is_pool=False,
                            is_horizontal=is_horizontal
                        ))

                # Save
                if pool is not None:
                    self.pool = pool

                # Keep only plausible lanes (avoid huge background containers)
                # Sort by area desc so we can assign smallest containing lane later.
                lanes.sort(key=lambda l: l.width * l.height, reverse=True)
                self.lanes = lanes
        except:
            return

    def _assign_shapes_to_lanes(self) -> None:
        """Assign each BPMN shape to the smallest containing lane by geometry."""
        if not self.lanes or not self.shapes:
            return

        def contains(l: BPMNLane, s: BPMNShape) -> bool:
            # Use Visio coordinate system (not flipped): lane bounds based on PinX/PinY.
            lx0 = l.x - l.width / 2
            lx1 = l.x + l.width / 2
            ly0 = l.y - l.height / 2
            ly1 = l.y + l.height / 2
            return (lx0 <= s.x <= lx1) and (ly0 <= s.y <= ly1)

        for shape in self.shapes:
            candidates = [l for l in self.lanes if contains(l, shape)]
            if not candidates:
                continue
            # Choose the smallest area lane that still contains the shape
            best = sorted(candidates, key=lambda l: l.width * l.height)[0]
            shape.lane_id = best.id
            if self._is_flow_node(shape):
                best.flow_node_refs.append(shape.id)

    def _is_flow_node(self, shape: BPMNShape) -> bool:
        """True if this BPMN element can be referenced by lane.flowNodeRef."""
        t = (shape.shape_type or '')
        return any([
            t.endswith('Event'),
            t.endswith('Gateway'),
            t in ('task', 'subProcess')
        ])

    def _get_position(self, shape) -> Tuple[float, float]:
        """Get shape position in pixels"""
        try:
            pinx = shape.cells.get('PinX')
            piny = shape.cells.get('PinY')
            x = float(getattr(pinx, 'value', 0.0)) * self.PX_PER_INCH  # inches to pixels
            y = float(getattr(piny, 'value', 0.0)) * self.PX_PER_INCH
            return (x, y)
        except:
            return (100, 100)

    def _get_size(self, shape) -> Tuple[float, float]:
        """Get shape size in pixels"""
        try:
            wcell = shape.cells.get('Width')
            hcell = shape.cells.get('Height')
            w = float(getattr(wcell, 'value', 1.0)) * self.PX_PER_INCH
            h = float(getattr(hcell, 'value', 0.5)) * self.PX_PER_INCH
            return (max(w, 80), max(h, 40))
        except:
            return (100, 80)

    def _try_load_page_dimensions_from_xml(self, page_number: int) -> None:
        """Populate page width/height from Visio page XML (in inches -> pixels)."""
        if self.page_height_px is not None and self.page_width_px is not None:
            return

        try:
            with zipfile.ZipFile(self.visio_path, 'r') as zf:
                page_xml_path = f"visio/pages/page{page_number}.xml"
                if page_xml_path not in zf.namelist():
                    return

                xml_text = zf.read(page_xml_path).decode('utf-8')
                ns = {'v': 'http://schemas.microsoft.com/office/visio/2012/main'}
                root = ET.fromstring(xml_text)
                page_sheet = root.find('v:PageSheet', ns)
                if page_sheet is None:
                    return

                def read_cell(n: str) -> Optional[float]:
                    cell = page_sheet.find(f"v:Cell[@N='{n}']", ns)
                    if cell is None:
                        return None
                    v = cell.attrib.get('V')
                    try:
                        return float(v) if v is not None else None
                    except:
                        return None

                w_in = read_cell('PageWidth')
                h_in = read_cell('PageHeight')
                if w_in and h_in:
                    self.page_width_px = w_in * self.PX_PER_INCH
                    self.page_height_px = h_in * self.PX_PER_INCH
        except:
            return

    def _extract_connections_from_page_xml(self, page_number: int, bpmn_id_map: Dict[str, str], visio_shapes_by_id: Dict[str, object]) -> List[BPMNConnection]:
        """Extract connector relationships from the page XML <Connects> table.

        Visio stores glue relationships like:
          Connect FromSheet="<connectorId>" FromCell="BeginX" ToSheet="<shapeId>" ...
          Connect FromSheet="<connectorId>" FromCell="EndX"   ToSheet="<shapeId>" ...
        """
        out: List[BPMNConnection] = []
        try:
            with zipfile.ZipFile(self.visio_path, 'r') as zf:
                page_xml_path = f"visio/pages/page{page_number}.xml"
                if page_xml_path not in zf.namelist():
                    return out

                xml_text = zf.read(page_xml_path).decode('utf-8')
                ns = {'v': 'http://schemas.microsoft.com/office/visio/2012/main'}
                root = ET.fromstring(xml_text)

                connects = root.find('v:Connects', ns)
                if connects is None:
                    return out

                # connector_id -> {'begin': shape_id, 'end': shape_id}
                links: Dict[str, Dict[str, str]] = {}
                for c in connects.findall('v:Connect', ns):
                    from_sheet = c.attrib.get('FromSheet')
                    to_sheet = c.attrib.get('ToSheet')
                    from_cell = (c.attrib.get('FromCell') or '').lower()
                    if not from_sheet or not to_sheet:
                        continue

                    if from_cell.startswith('begin'):
                        links.setdefault(from_sheet, {})['begin'] = to_sheet
                    elif from_cell.startswith('end'):
                        links.setdefault(from_sheet, {})['end'] = to_sheet

                # Ensure we have a page height for Y flip; fall back to derived bounds
                if self.page_height_px is None:
                    # Derive from max PinY if page data is missing
                    max_y = 0.0
                    for s in visio_shapes_by_id.values():
                        try:
                            piny = s.cells.get('PinY')
                            y = float(getattr(piny, 'value', 0.0)) * self.PX_PER_INCH
                            max_y = max(max_y, y)
                        except:
                            pass
                    self.page_height_px = max(max_y + 200.0, 800.0)

                for connector_id, lr in links.items():
                    from_visio = lr.get('begin')
                    to_visio = lr.get('end')
                    if not from_visio or not to_visio:
                        continue

                    source_id = bpmn_id_map.get(from_visio)
                    target_id = bpmn_id_map.get(to_visio)
                    if not source_id or not target_id or source_id == target_id:
                        continue

                    conn = BPMNConnection(
                        id=f"Flow_{connector_id}",
                        source_id=source_id,
                        target_id=target_id,
                        name=""
                    )

                    # Waypoints: generate clean orthogonal routing based on node bounds.
                    conn.waypoints = self._route_connection_orthogonal(source_id, target_id)

                    out.append(conn)

        except Exception:
            return out

        return out

    def _route_connection_orthogonal(self, source_bpmn_id: str, target_bpmn_id: str) -> List[Tuple[float, float]]:
        """Create a clean right-angled route between two shapes in DI coordinates.

        This sacrifices exact Visio connector geometry but produces much cleaner bpmn-js rendering.
        """
        page_h = float(self.page_height_px or 800.0)

        src = next((s for s in self.shapes if s.id == source_bpmn_id), None)
        tgt = next((s for s in self.shapes if s.id == target_bpmn_id), None)
        if not src or not tgt:
            return []

        def bounds_di(s: BPMNShape) -> Tuple[float, float, float, float]:
            # same logic as _shape_to_di
            x = s.x - s.width / 2
            y = page_h - s.y - s.height / 2
            # event/gateway sizing adjustments
            if 'Event' in s.shape_type:
                w, h = 36.0, 36.0
            elif 'Gateway' in s.shape_type:
                w, h = 50.0, 50.0
            else:
                w, h = float(s.width), float(s.height)
            return (x, y, w, h)

        sx, sy, sw, sh = bounds_di(src)
        tx, ty, tw, th = bounds_di(tgt)
        scx, scy = sx + sw / 2, sy + sh / 2
        tcx, tcy = tx + tw / 2, ty + th / 2

        dx = tcx - scx
        dy = tcy - scy
        pad = 12.0

        # pick exit/entry sides
        if abs(dx) >= abs(dy):
            # left-right
            sp = (sx + sw + pad, scy) if dx >= 0 else (sx - pad, scy)
            tp = (tx - pad, tcy) if dx >= 0 else (tx + tw + pad, tcy)
            midx = (sp[0] + tp[0]) / 2
            pts = [sp, (midx, sp[1]), (midx, tp[1]), tp]
        else:
            # top-bottom
            sp = (scx, sy + sh + pad) if dy >= 0 else (scx, sy - pad)
            tp = (tcx, ty - pad) if dy >= 0 else (tcx, ty + th + pad)
            midy = (sp[1] + tp[1]) / 2
            pts = [sp, (sp[0], midy), (tp[0], midy), tp]

        # collapse redundant points (same x or y)
        simplified: List[Tuple[float, float]] = []
        for p in pts:
            if not simplified:
                simplified.append(p)
                continue
            lx, ly = simplified[-1]
            if abs(lx - p[0]) < 0.1 and abs(ly - p[1]) < 0.1:
                continue
            simplified.append(p)
        return simplified

    def generate_bpmn_xml(self) -> str:
        """Generate BPMN 2.0 XML from extracted shapes"""
        process_id = f"Process_{uuid.uuid4().hex[:8]}"

        # If we have a pool, emit a Collaboration + Participant (pool)
        collaboration_id = None
        participant_id = None
        if self.pool is not None:
            collaboration_id = f"Collaboration_{uuid.uuid4().hex[:8]}"
            participant_id = self.pool.id or f"Participant_{uuid.uuid4().hex[:8]}"

        # Start building XML
        xml_lines = [
            '<?xml version="1.0" encoding="UTF-8"?>',
            '<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"',
            '                  xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI"',
            '                  xmlns:dc="http://www.omg.org/spec/DD/20100524/DC"',
            '                  xmlns:di="http://www.omg.org/spec/DD/20100524/DI"',
            f'                  id="Definitions_{uuid.uuid4().hex[:8]}"',
            '                  targetNamespace="http://bpmn.io/schema/bpmn">',
        ]

        # Add process
        xml_lines.append(f'  <bpmn:process id="{process_id}" name="{self._escape_xml(self.process_name)}" isExecutable="false">')

        # Add flow elements
        for shape in self.shapes:
            xml_lines.append(self._shape_to_bpmn_element(shape))

        # Add sequence flows
        for conn in self.connections:
            name_attr = f' name="{self._escape_xml(conn.name)}"' if conn.name else ''
            xml_lines.append(f'    <bpmn:sequenceFlow id="{conn.id}" sourceRef="{conn.source_id}" targetRef="{conn.target_id}"{name_attr}/>')

        # Add lanes (if any)
        if self.lanes:
            xml_lines.append('    <bpmn:laneSet id="LaneSet_1">')
            for lane in self.lanes:
                lname = self._escape_xml(lane.name or '')
                xml_lines.append(f'      <bpmn:lane id="{lane.id}" name="{lname}">')
                # flowNodeRef: only for actual flow nodes
                for fr in lane.flow_node_refs:
                    xml_lines.append(f'        <bpmn:flowNodeRef>{fr}</bpmn:flowNodeRef>')
                xml_lines.append('      </bpmn:lane>')
            xml_lines.append('    </bpmn:laneSet>')

        xml_lines.append('  </bpmn:process>')

        # Collaboration / Participant (pool)
        if collaboration_id and participant_id:
            pname = self._escape_xml(self.pool.name if self.pool else self.process_name)
            xml_lines.append(f'  <bpmn:collaboration id="{collaboration_id}">')
            xml_lines.append(f'    <bpmn:participant id="{participant_id}" name="{pname}" processRef="{process_id}"/>')
            xml_lines.append('  </bpmn:collaboration>')

        # Add diagram (DI)
        xml_lines.append(f'  <bpmndi:BPMNDiagram id="BPMNDiagram_1">')
        plane_ref = collaboration_id if collaboration_id else process_id
        xml_lines.append(f'    <bpmndi:BPMNPlane id="BPMNPlane_1" bpmnElement="{plane_ref}">')

        # Pool + Lane DI first (so they render behind flow nodes)
        if collaboration_id and participant_id and self.pool is not None:
            xml_lines.append(self._lane_or_pool_to_di(self.pool))

        for lane in self.lanes:
            xml_lines.append(self._lane_or_pool_to_di(lane))

        # Add shape DI
        for shape in self.shapes:
            xml_lines.append(self._shape_to_di(shape))

        # Add edge DI
        for conn in self.connections:
            xml_lines.append(self._connection_to_di(conn))

        xml_lines.append('    </bpmndi:BPMNPlane>')
        xml_lines.append('  </bpmndi:BPMNDiagram>')
        xml_lines.append('</bpmn:definitions>')

        return '\n'.join(xml_lines)

    def _lane_or_pool_to_di(self, lane: BPMNLane) -> str:
        """Generate BPMN DI for lane/pool bounds."""
        page_h = float(self.page_height_px or 800.0)
        x = lane.x - lane.width / 2
        y = page_h - lane.y - lane.height / 2
        horiz_attr = ''
        if not lane.is_pool:
            horiz_attr = f' isHorizontal="{str(lane.is_horizontal).lower()}"'
        return f'''      <bpmndi:BPMNShape id="{lane.id}_di" bpmnElement="{lane.id}"{horiz_attr}>
        <dc:Bounds x="{x:.0f}" y="{y:.0f}" width="{lane.width:.0f}" height="{lane.height:.0f}"/>
      </bpmndi:BPMNShape>'''

    def _shape_to_bpmn_element(self, shape: BPMNShape) -> str:
        """Convert shape to BPMN element XML"""
        name_attr = f' name="{self._escape_xml(shape.name)}"' if shape.name else ''
        return f'    <bpmn:{shape.shape_type} id="{shape.id}"{name_attr}/>'

    def _shape_to_di(self, shape: BPMNShape) -> str:
        """Generate BPMN DI for shape"""
        # Adjust coordinates for BPMN (flip Y axis)
        x = shape.x - shape.width / 2
        page_h = float(self.page_height_px or 800.0)
        y = page_h - shape.y - shape.height / 2  # Flip Y

        # Events are circles (36x36), gateways are diamonds (50x50)
        if 'Event' in shape.shape_type:
            w, h = 36, 36
        elif 'Gateway' in shape.shape_type:
            w, h = 50, 50
        else:
            w, h = shape.width, shape.height

        return f'''      <bpmndi:BPMNShape id="{shape.id}_di" bpmnElement="{shape.id}">
        <dc:Bounds x="{x:.0f}" y="{y:.0f}" width="{w:.0f}" height="{h:.0f}"/>
        <bpmndi:BPMNLabel/>
      </bpmndi:BPMNShape>'''

    def _connection_to_di(self, conn: BPMNConnection) -> str:
        """Generate BPMN DI for connection"""
        page_h = float(self.page_height_px or 800.0)

        waypoints = ""
        if conn.waypoints and len(conn.waypoints) >= 2:
            wp_xml = []
            for (x, y) in conn.waypoints:
                wp_xml.append(f'        <di:waypoint x="{x:.0f}" y="{y:.0f}"/>')
            waypoints = "\n" + "\n".join(wp_xml)
        else:
            # Fallback: straight line between centers
            source = next((s for s in self.shapes if s.id == conn.source_id), None)
            target = next((s for s in self.shapes if s.id == conn.target_id), None)
            if source and target:
                sx = source.x
                sy = page_h - source.y
                tx = target.x
                ty = page_h - target.y
                waypoints = f'''\n        <di:waypoint x="{sx:.0f}" y="{sy:.0f}"/>\n        <di:waypoint x="{tx:.0f}" y="{ty:.0f}"/>'''

        return f'''      <bpmndi:BPMNEdge id="{conn.id}_di" bpmnElement="{conn.id}">{waypoints}
      </bpmndi:BPMNEdge>'''

    def _escape_xml(self, text: str) -> str:
        """Escape special XML characters"""
        if not text:
            return ""
        return (text
            .replace('&', '&amp;')
            .replace('<', '&lt;')
            .replace('>', '&gt;')
            .replace('"', '&quot;')
            .replace("'", '&apos;')
        )

    def convert(self) -> Optional[str]:
        """Main conversion method"""
        if self.extract_from_visio():
            if self.shapes:
                return self.generate_bpmn_xml()
            else:
                print(f"No shapes found in {self.visio_path}")
        return None


def batch_convert(input_folder: str, output_folder: str) -> Dict:
    """Batch convert all Visio files in a folder"""
    input_path = Path(input_folder)
    output_path = Path(output_folder)
    output_path.mkdir(parents=True, exist_ok=True)

    results = {
        'success': 0,
        'failed': 0,
        'files': []
    }

    visio_files = list(input_path.glob('*.vsdx'))
    print(f"Found {len(visio_files)} Visio files to convert")

    for vsdx_file in visio_files:
        print(f"Converting: {vsdx_file.name}...")

        converter = VisioToBPMNConverter(str(vsdx_file))
        bpmn_xml = converter.convert()

        if bpmn_xml:
            output_file = output_path / f"{vsdx_file.stem}.bpmn"
            with open(output_file, 'w', encoding='utf-8') as f:
                f.write(bpmn_xml)

            results['success'] += 1
            results['files'].append({
                'source': vsdx_file.name,
                'output': output_file.name,
                'shapes': len(converter.shapes),
                'connections': len(converter.connections)
            })
            print(f"  ✓ Converted: {len(converter.shapes)} shapes, {len(converter.connections)} connections")
        else:
            results['failed'] += 1
            print(f"  ✗ Failed to convert")

    return results


def main():
    """Main entry point"""
    # Paths
    input_folder = r"C:\Users\kalmi\OneDrive\Desktop\MB1\ExtractedVisio"
    output_folder = r"C:\Users\kalmi\OneDrive\Desktop\MB1\ConvertedBPMN"

    print("=" * 60)
    print("Visio to BPMN 2.0 Converter")
    print("=" * 60)

    results = batch_convert(input_folder, output_folder)

    print("\n" + "=" * 60)
    print("Conversion Summary:")
    print(f"  Success: {results['success']}")
    print(f"  Failed:  {results['failed']}")
    print(f"  Output:  {output_folder}")
    print("=" * 60)

    # Save results
    results_file = Path(output_folder) / "conversion_results.json"
    with open(results_file, 'w', encoding='utf-8') as f:
        json.dump(results, f, indent=2, ensure_ascii=False)
    print(f"\nResults saved to: {results_file}")


if __name__ == "__main__":
    main()


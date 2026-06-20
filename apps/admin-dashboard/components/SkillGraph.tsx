'use client';

/**
 * SkillGraph — accessible list/adjacency prerequisite graph editor (P7-03 §S4).
 *
 * This is the core of P7-03. It is an ACCESSIBLE LIST/ADJACENCY EDITOR — NOT a
 * canvas or drag-to-connect graph. Design decision D1 is LOCKED (plan §D1 + brief OQ-2).
 * No graph-viz library, no drag library, no new dependency.
 *
 * Layout:
 *   - Node list (role=listbox, roving tabindex, Arrow Up/Down, Enter/Space)
 *   - Selected node detail panel:
 *       - Prerequisites section (incoming edges, from graph.edges client-side derivation)
 *       - Unlocks section (outgoing edges, same derivation)
 *       - Add Prerequisite control (native <select> + Add button)
 *       - Skill Detail section (if nodeType === Skill)
 *
 * Edge add/remove flow:
 *   - POST /api/Learning/KnowledgeGraph/Edges → refetch graph on success
 *   - DELETE /api/Learning/KnowledgeGraph/Edges/{edgeId} → refetch graph on success
 *   - No optimistic updates (plan D8 / brief contract)
 *   - Cycle/cross-language/duplicate rejections mapped via mapEdgeError()
 *     (mirrors SuspendUserDialog.mapSuspendError exactly)
 *
 * A11y:
 *   - role=listbox + role=option (roving tabindex, Arrow Up/Down, Enter/Space, Home/End)
 *   - aria-live="polite" (outside card) for successful edge add/remove
 *   - aria-live="assertive" (inline error region) for rejections
 *   - All mutation controls disabled during in-flight mutation (concurrent race prevention)
 *
 * Node-id space: ALL edge operations use KnowledgeNode.id (not skillId).
 * Skill metadata is resolved from graph.nodes by matching node.skillId.
 */

import { useState, useRef, useCallback, useEffect } from 'react';
import {
  useSkillGraph,
  useAddKnowledgeEdge,
  useRemoveKnowledgeEdge,
  type KnowledgeEdgeDto,
  type SingleSkillResponse,
} from '@learnexia/api-client';
import { isApiError } from '@learnexia/api-client/client';
import { NODE_TYPE, RELATIONSHIP_TYPE } from '@learnexia/shared/constants';
import type { SubjectDto } from '@learnexia/api-client';
import { AdminErrorBanner } from './AdminErrorBanner';
import { ActiveBadge } from './ActiveBadge';
import { SubjectCodeBadge } from './SubjectCodeBadge';
import { LanguageBadge } from './LanguageBadge';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

// ── Error mapping (mirrors SuspendUserDialog.mapSuspendError) ─────────────────

function mapEdgeError(err: unknown): string {
  if (isApiError(err) && err.status === 400) {
    const msg = err.message ?? '';
    if (msg.includes('cycle') || msg.includes('KnowledgeEdgeWouldCreateCycle'))
      return strings.skillGraphErrCycle;
    if (msg.includes('cross-language') || msg.includes('KnowledgeEdgeCrossLanguageForbidden'))
      return strings.skillGraphErrCrossLanguage;
    if (msg.includes('already exists') || msg.includes('KnowledgeEdgeDuplicate'))
      return strings.skillGraphErrDuplicate;
    if (msg.includes('node was not found') || msg.includes('KnowledgeNodeNotFound'))
      return strings.skillGraphErrNodeNotFound;
    if (msg.includes('KnowledgeNodeSubjectNotResolvable'))
      return strings.skillGraphErrSubjectUnresolvable;
    if (msg.includes('strength') || msg.includes('KnowledgeEdgeStrengthOutOfRange'))
      return strings.skillGraphErrStrengthOutOfRange;
    return msg || strings.skillGraphErrGeneric;
  }
  if (isApiError(err) && err.status === 404) return strings.skillGraphErrNodeNotFound;
  return strings.skillGraphErrNetwork;
}

// ── Node type pip ──────────────────────────────────────────────────────────────

function NodeTypePip({ nodeType }: { nodeType: number }) {
  const color =
    nodeType === NODE_TYPE.Skill ? 'var(--lx-primary)' :
    nodeType === NODE_TYPE.Concept ? 'var(--lx-accent)' :
    'var(--lx-secondary)';
  return (
    <span
      aria-hidden
      style={{
        width: 8, height: 8, borderRadius: 9999,
        backgroundColor: color, flexShrink: 0, display: 'inline-block',
      }}
    />
  );
}

// ── Inline SVG icons ──────────────────────────────────────────────────────────

function GitForkIcon() {
  return (
    <svg width="40" height="40" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="6" cy="18" r="2" /><circle cx="18" cy="18" r="2" /><circle cx="12" cy="6" r="2" />
      <path d="M6 16v-4a6 6 0 0 1 12 0v4" />
    </svg>
  );
}

function PlusCircleIcon({ size = 14 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" /><line x1="12" y1="8" x2="12" y2="16" /><line x1="8" y1="12" x2="16" y2="12" />
    </svg>
  );
}

function PlusIcon({ size = 14 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <line x1="12" y1="5" x2="12" y2="19" /><line x1="5" y1="12" x2="19" y2="12" />
    </svg>
  );
}

function XIcon({ size = 13 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <line x1="18" y1="6" x2="6" y2="18" /><line x1="6" y1="6" x2="18" y2="18" />
    </svg>
  );
}

function ChevronRightIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <polyline points="9 18 15 12 9 6" />
    </svg>
  );
}

function PencilIcon({ size = 13 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
      <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
    </svg>
  );
}

// ── Skeleton rows ─────────────────────────────────────────────────────────────

function GraphSkeleton() {
  return (
    <div role="status" aria-label={strings.skillGraphLoading}
      data-testid="graph-loading"
      style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
      {[1, 2, 3].map((i) => (
        <div key={i} style={{
          height: 48, borderRadius: 8,
          background: 'linear-gradient(90deg, var(--lx-card-soft) 25%, var(--lx-card) 50%, var(--lx-card-soft) 75%)',
          backgroundSize: '400px 100%',
          animation: 'lx-shimmer 1400ms linear infinite',
        }} />
      ))}
    </div>
  );
}

// ── Props ─────────────────────────────────────────────────────────────────────

export interface SkillGraphProps {
  subjectId: number;
  selectedSubject: SubjectDto | null;
  /** Full skills list — used to resolve skill metadata from node.skillId. */
  skills: SingleSkillResponse[];
  /** Called when user clicks "Edit this skill" from the detail panel. */
  onEditSkill?: (skill: SingleSkillResponse) => void;
  /** Called when the selected skill changes (so the skills table can highlight). */
  onSelectSkillId?: (skillId: number | null) => void;
  /**
   * Externally controlled selected skill id — set by the skills-table row
   * selection (Finding 1: §1.6 bi-directional wiring).
   * When this prop changes, the graph resolves the corresponding node and
   * activates it (so the prerequisites/unlocks panel reflects the row click).
   * If no matching node is found, selection is left unchanged.
   */
  externalSelectedSkillId?: number | null;
}

// ── Main component ────────────────────────────────────────────────────────────

export function SkillGraph({
  subjectId,
  selectedSubject,
  skills,
  onEditSkill,
  onSelectSkillId,
  externalSelectedSkillId,
}: SkillGraphProps) {
  const [selectedNodeId, setSelectedNodeId] = useState<number | null>(null);
  const [pickerValue, setPickerValue] = useState<string>('');
  const [edgeErrorMsg, setEdgeErrorMsg] = useState<string | null>(null);
  const [announcementMsg, setAnnouncementMsg] = useState<string>('');
  const listboxRef = useRef<HTMLDivElement>(null);

  const graphQuery = useSkillGraph(subjectId);
  const addEdgeMutation = useAddKnowledgeEdge();
  const removeEdgeMutation = useRemoveKnowledgeEdge();

  const isEdgeMutating = addEdgeMutation.isPending || removeEdgeMutation.isPending;

  const graph = graphQuery.data;

  // Clear selection when subject changes
  useEffect(() => {
    setSelectedNodeId(null);
    setPickerValue('');
    setEdgeErrorMsg(null);
    onSelectSkillId?.(null);
  }, [subjectId, onSelectSkillId]);

  // Finding 1 — wire external skill-table row selection into the graph node list.
  // When the parent page sets externalSelectedSkillId (via row click), resolve
  // the matching graph node and activate it so the prerequisites/unlocks panel
  // reflects the selection. If no node matches (e.g. skill has no graph node),
  // leave the current selection unchanged to avoid crashing.
  useEffect(() => {
    if (!graph) return;
    if (externalSelectedSkillId == null) {
      // A null skill id can mean two different things:
      //   (a) the table row was deselected → clear the graph selection too, or
      //   (b) the graph just selected a *concept* node, which has no skillId and
      //       therefore reported `null` back up via onSelectSkillId.
      // In case (b) we must NOT clear: that would wipe the selection the user just
      // made by keyboard/click on a concept node (the CUR-TC-71 defect). Keep the
      // selection whenever the currently-selected node is a non-skill node.
      const current = graph.nodes.find((n) => n.id === selectedNodeId);
      if (current && current.skillId == null) {
        return;
      }
      setSelectedNodeId(null);
      setPickerValue('');
      setEdgeErrorMsg(null);
      return;
    }
    const matchingNode = graph.nodes.find((n) => n.skillId === externalSelectedSkillId);
    if (!matchingNode) return; // no corresponding node — leave selection unchanged
    if (matchingNode.id === selectedNodeId) return; // already selected — no-op
    setSelectedNodeId(matchingNode.id);
    setPickerValue('');
    setEdgeErrorMsg(null);
    // Focus the newly selected item in the listbox for keyboard users
    setTimeout(() => {
      const el = listboxRef.current?.querySelector<HTMLElement>(`[data-node-id="${matchingNode.id}"]`);
      el?.focus();
    }, 0);
  }, [externalSelectedSkillId, graph]);

  // Derived data
  const selectedNode = graph?.nodes.find((n) => n.id === selectedNodeId) ?? null;

  const prerequisiteEdges: KnowledgeEdgeDto[] = selectedNodeId != null && graph
    ? graph.edges.filter(
        (e) => e.targetNodeId === selectedNodeId && e.relationshipType === RELATIONSHIP_TYPE.Prerequisite,
      )
    : [];

  const unlocksEdges: KnowledgeEdgeDto[] = selectedNodeId != null && graph
    ? graph.edges.filter(
        (e) => e.sourceNodeId === selectedNodeId && e.relationshipType === RELATIONSHIP_TYPE.Prerequisite,
      )
    : [];

  // Already-prerequisite node ids (to exclude from picker)
  const alreadyPrereqNodeIds = new Set(prerequisiteEdges.map((e) => e.sourceNodeId));

  // Picker options: same-tree nodes, excluding self + already-prerequisites
  const pickerOptions = (graph?.nodes ?? []).filter(
    (n) => n.id !== selectedNodeId && !alreadyPrereqNodeIds.has(n.id),
  );

  const resolveSkill = (skillId: number | null | undefined): SingleSkillResponse | null => {
    if (!skillId) return null;
    return skills.find((s) => s.id === skillId) ?? null;
  };

  const resolveNodeName = (nodeId: number): string => {
    return graph?.nodes.find((n) => n.id === nodeId)?.name ?? `Node ${nodeId}`;
  };

  // ── Listbox keyboard navigation ───────────────────────────────────────────

  const handleListboxKeyDown = useCallback(
    (e: React.KeyboardEvent<HTMLDivElement>) => {
      if (!graph?.nodes.length) return;
      const nodes = graph.nodes;
      const currentIdx = selectedNodeId != null ? nodes.findIndex((n) => n.id === selectedNodeId) : -1;

      switch (e.key) {
        case 'ArrowDown': {
          e.preventDefault();
          const next = currentIdx < nodes.length - 1 ? currentIdx + 1 : 0;
          handleSelectNode(nodes[next]!.id);
          break;
        }
        case 'ArrowUp': {
          e.preventDefault();
          const prev = currentIdx > 0 ? currentIdx - 1 : nodes.length - 1;
          handleSelectNode(nodes[prev]!.id);
          break;
        }
        case 'Home': {
          e.preventDefault();
          handleSelectNode(nodes[0]!.id);
          break;
        }
        case 'End': {
          e.preventDefault();
          handleSelectNode(nodes[nodes.length - 1]!.id);
          break;
        }
        case 'Enter':
        case ' ': {
          e.preventDefault();
          if (selectedNodeId != null) handleSelectNode(selectedNodeId);
          break;
        }
      }
    },
    [graph, selectedNodeId],
  );

  const handleSelectNode = (nodeId: number) => {
    setSelectedNodeId(nodeId);
    setPickerValue('');
    setEdgeErrorMsg(null);
    const node = graph?.nodes.find((n) => n.id === nodeId);
    onSelectSkillId?.(node?.skillId ?? null);

    // Focus the selected item in the listbox
    setTimeout(() => {
      const el = listboxRef.current?.querySelector<HTMLElement>(`[data-node-id="${nodeId}"]`);
      el?.focus();
    }, 0);
  };

  const handleDeselectNode = () => {
    setSelectedNodeId(null);
    setPickerValue('');
    setEdgeErrorMsg(null);
    onSelectSkillId?.(null);
  };

  // ── Add edge ──────────────────────────────────────────────────────────────

  const handleAddEdge = async () => {
    if (!pickerValue || selectedNodeId == null) return;
    const sourceNodeId = parseInt(pickerValue, 10);
    setEdgeErrorMsg(null);

    try {
      await addEdgeMutation.mutateAsync({
        sourceNodeId,
        targetNodeId: selectedNodeId,
        relationshipType: RELATIONSHIP_TYPE.Prerequisite,
        strength: 1.0,
        _subjectId: subjectId,
        _targetNodeId: selectedNodeId,
      });
      setPickerValue('');
      const addedName = resolveNodeName(sourceNodeId);
      setAnnouncementMsg(strings.skillGraphEdgeAdded.replace('{name}', addedName));
    } catch (err) {
      setEdgeErrorMsg(mapEdgeError(err));
    }
  };

  // ── Remove edge ───────────────────────────────────────────────────────────

  const handleRemoveEdge = async (edge: KnowledgeEdgeDto) => {
    setEdgeErrorMsg(null);
    const removedName = resolveNodeName(edge.sourceNodeId);

    try {
      await removeEdgeMutation.mutateAsync({
        edgeId: edge.id,
        subjectId,
        targetNodeId: edge.targetNodeId,
        sourceNodeId: edge.sourceNodeId,
      });
      setAnnouncementMsg(strings.skillGraphEdgeRemoved.replace('{name}', removedName));
    } catch (err) {
      setEdgeErrorMsg(mapEdgeError(err));
    }
  };

  // ── No subject selected ───────────────────────────────────────────────────

  if (!subjectId || subjectId <= 0) {
    return (
      <div data-testid="graph-no-subject" style={{
        backgroundColor: 'var(--lx-card)',
        borderRadius: 20,
        border: '1px solid var(--lx-border)',
        padding: 40,
        display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 16,
      }}>
        <span style={{ color: 'var(--lx-fg3)' }}><GitForkIcon /></span>
        <h3 style={{ margin: 0, fontSize: 18, fontWeight: 600, color: 'var(--lx-fg1)' }}>
          {strings.skillGraphNoSubject}
        </h3>
        <p style={{ margin: 0, fontSize: 14, color: 'var(--lx-fg3)', textAlign: 'center', maxWidth: 280 }}>
          {strings.skillGraphNoSubjectBody}
        </p>
      </div>
    );
  }

  // ── Loading ───────────────────────────────────────────────────────────────

  if (graphQuery.isLoading) {
    return (
      <div style={{
        backgroundColor: 'var(--lx-card)', borderRadius: 20,
        border: '1px solid var(--lx-border)', padding: 24,
      }}>
        <GraphSkeleton />
      </div>
    );
  }

  // ── Error ─────────────────────────────────────────────────────────────────

  if (graphQuery.isError) {
    return (
      <div data-testid="graph-error-banner" style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
        <AdminErrorBanner variant="error" message={strings.skillGraphError} />
        <button
          onClick={() => void graphQuery.refetch()}
          style={{
            height: 36, paddingLeft: 16, paddingRight: 16, borderRadius: 16,
            border: '1px solid rgba(255,255,255,0.16)', backgroundColor: 'transparent',
            color: 'var(--lx-fg2)', fontSize: 14, cursor: 'pointer', fontFamily: 'inherit',
          }}
        >
          {strings.skillGraphRetry}
        </button>
      </div>
    );
  }

  // ── Results ───────────────────────────────────────────────────────────────

  const nodes = graph?.nodes ?? [];
  const edges = graph?.edges ?? [];

  return (
    <>
      {/* Polite aria-live for successful edge add/remove — OUTSIDE the graph card */}
      <span
        id="graph-announcement"
        aria-live="polite"
        aria-atomic="true"
        className="sr-only"
        style={{ position: 'absolute', width: 1, height: 1, overflow: 'hidden', clip: 'rect(0,0,0,0)', whiteSpace: 'nowrap' }}
      >
        {announcementMsg}
      </span>

      <div style={{
        backgroundColor: 'var(--lx-card)',
        borderRadius: 20,
        border: '1px solid var(--lx-border)',
        padding: 20,
        display: 'flex', flexDirection: 'column', gap: 16,
      }}>
        {/* Editor header */}
        <div style={{
          display: 'flex', alignItems: 'center', justifyContent: 'space-between',
          gap: 12, paddingBottom: 12, borderBottom: '1px solid var(--lx-border)',
          flexWrap: 'wrap',
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
            <h3 style={{ margin: 0, fontSize: 16, fontWeight: 700, color: 'var(--lx-fg1)' }}>
              {strings.skillGraphTitle}
            </h3>
            {selectedSubject && (
              <>
                <SubjectCodeBadge code={selectedSubject.subjectCode} />
                <LanguageBadge language={selectedSubject.language} />
              </>
            )}
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            {nodes.length > 0 && (
              <span style={{
                backgroundColor: 'var(--lx-card-soft)', borderRadius: 9999,
                paddingLeft: 10, paddingRight: 10, height: 20, display: 'inline-flex',
                alignItems: 'center', fontSize: 12, color: 'var(--lx-fg3)',
              }}>
                {nodes.length} {strings.skillGraphNodeCount}
              </span>
            )}
            {edges.length > 0 && (
              <span style={{
                backgroundColor: 'var(--lx-card-soft)', borderRadius: 9999,
                paddingLeft: 10, paddingRight: 10, height: 20, display: 'inline-flex',
                alignItems: 'center', fontSize: 12, color: 'var(--lx-fg3)',
              }}>
                {edges.length} {strings.skillGraphEdgeCount}
              </span>
            )}
          </div>

          {/* Inline error region — assertive, for rejections */}
          {edgeErrorMsg && (
            <div
              role="alert"
              aria-live="assertive"
              aria-atomic="true"
              data-testid="graph-edge-error"
              style={{ width: '100%', minHeight: 0 }}
            >
              <AdminErrorBanner variant="error" message={edgeErrorMsg} />
            </div>
          )}
        </div>

        {/* Node listbox */}
        <div
          ref={listboxRef}
          role="listbox"
          aria-label={strings.skillGraphNodeListLabel}
          aria-multiselectable="false"
          tabIndex={0}
          data-testid="graph-node-listbox"
          onKeyDown={handleListboxKeyDown}
          style={{
            display: 'flex', flexDirection: 'column', gap: 4,
            maxHeight: 360, overflowY: 'auto',
            borderRadius: 8, border: '1px solid var(--lx-border)',
            outline: 'none',
          }}
        >
          {nodes.length === 0 ? (
            <div data-testid="graph-nodes-empty"
              style={{ fontSize: 14, color: 'var(--lx-fg3)', padding: 20, textAlign: 'center' }}>
              {strings.skillGraphNodesEmpty}
            </div>
          ) : (
            nodes.map((node, idx) => {
              const isSelected = node.id === selectedNodeId;
              return (
                <div
                  key={node.id}
                  role="option"
                  aria-selected={isSelected}
                  tabIndex={idx === 0 ? 0 : -1}
                  data-node-id={node.id}
                  data-testid={`graph-node-${node.id}`}
                  aria-label={`${node.name} — ${node.nodeType === NODE_TYPE.Skill ? strings.skillGraphNodeTypeSkill : node.nodeType === NODE_TYPE.Concept ? strings.skillGraphNodeTypeConcept : strings.skillGraphNodeTypeReview}${node.isSkillActive === false ? ' — inactive' : ''}`}
                  onClick={() => handleSelectNode(node.id)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                      e.preventDefault();
                      handleSelectNode(node.id);
                    }
                  }}
                  style={{
                    display: 'flex', alignItems: 'center', gap: 10,
                    padding: '10px 12px',
                    cursor: 'pointer',
                    borderBottom: '1px solid var(--lx-border)',
                    backgroundColor: isSelected ? 'rgba(79,70,229,0.18)' : 'transparent',
                    borderInlineStart: isSelected ? '3px solid var(--lx-primary)' : '3px solid transparent',
                    transition: 'background-color 120ms var(--lx-ease-out)',
                    outline: 'none',
                  }}
                >
                  <NodeTypePip nodeType={node.nodeType} />
                  <span style={{
                    flex: 1, fontSize: 14,
                    fontWeight: node.nodeType === NODE_TYPE.Skill ? 600 : 500,
                    color: node.nodeType === NODE_TYPE.Skill
                      ? (node.isSkillActive === false ? 'rgba(248,250,252,0.5)' : 'var(--lx-fg1)')
                      : node.nodeType === NODE_TYPE.Concept ? 'var(--lx-fg2)' : 'var(--lx-fg3)',
                  }}>
                    {node.name}
                  </span>
                  {node.isSkillActive === false && (
                    <ActiveBadge isActive={false} />
                  )}
                  <span style={{ color: 'var(--lx-fg3)' }}><ChevronRightIcon /></span>
                </div>
              );
            })
          )}
        </div>

        {/* Selected node detail panel */}
        {selectedNode && (
          <div style={{
            display: 'flex', flexDirection: 'column', gap: 12,
            paddingTop: 12, borderTop: '1px solid var(--lx-border)',
          }}>
            {/* Panel heading */}
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <NodeTypePip nodeType={selectedNode.nodeType} />
              <span style={{ fontSize: 15, fontWeight: 700, color: 'var(--lx-fg1)', flex: 1 }}>
                {selectedNode.name}
              </span>
              <button
                onClick={handleDeselectNode}
                data-testid="graph-deselect-node"
                aria-label={strings.skillGraphDeselectNode}
                style={{
                  height: 24, width: 24, borderRadius: 8,
                  border: 'none', backgroundColor: 'transparent',
                  color: 'var(--lx-fg3)', cursor: 'pointer',
                  display: 'flex', alignItems: 'center', justifyContent: 'center',
                  marginInlineStart: 'auto', fontFamily: 'inherit',
                }}
              >
                <XIcon size={14} />
              </button>
            </div>

            {/* Prerequisites section */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                <span style={{ color: 'var(--lx-fg3)', fontSize: 13, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.04em' }}>
                  {strings.skillGraphPrerequisitesHeading}
                </span>
                {prerequisiteEdges.length > 0 && (
                  <span style={{
                    backgroundColor: 'var(--lx-card-soft)', borderRadius: 9999,
                    paddingLeft: 8, paddingRight: 8, height: 18, display: 'inline-flex',
                    alignItems: 'center', fontSize: 11, color: 'var(--lx-fg3)',
                  }}>
                    {prerequisiteEdges.length}
                  </span>
                )}
              </div>
              <ul
                role="list"
                aria-label={strings.skillGraphPrerequisitesAriaLabel.replace('{name}', selectedNode.name)}
                style={{ margin: 0, padding: 0, listStyle: 'none', display: 'flex', flexDirection: 'column', gap: 6 }}
              >
                {prerequisiteEdges.length === 0 ? (
                  <li data-testid="prerequisites-empty"
                    style={{ fontSize: 13, color: 'var(--lx-fg3)', fontStyle: 'italic' }}>
                    {strings.skillGraphPrerequisitesEmpty}
                  </li>
                ) : (
                  prerequisiteEdges.map((edge) => {
                    const prereqNode = graph?.nodes.find((n) => n.id === edge.sourceNodeId);
                    return (
                      <li key={edge.id} role="listitem"
                        data-testid={`prerequisite-item-${edge.id}`}
                        style={{
                          display: 'flex', alignItems: 'center', gap: 8,
                          padding: '8px 10px',
                          backgroundColor: 'rgba(255,255,255,0.03)',
                          borderRadius: 8, border: '1px solid var(--lx-border)',
                        }}
                      >
                        {prereqNode && <NodeTypePip nodeType={prereqNode.nodeType} />}
                        <span style={{ fontSize: 13, fontWeight: 500, color: 'var(--lx-fg2)', flex: 1 }}>
                          {prereqNode?.name ?? resolveNodeName(edge.sourceNodeId)}
                        </span>
                        {/* Remove button */}
                        <button
                          onClick={() => void handleRemoveEdge(edge)}
                          disabled={isEdgeMutating}
                          aria-label={strings.skillGraphRemoveEdgeAriaLabel.replace('{name}', prereqNode?.name ?? '')}
                          data-testid={`remove-prerequisite-${edge.id}`}
                          style={{
                            height: 28, width: 28, borderRadius: 8,
                            border: '1px solid rgba(239,68,68,0.2)',
                            backgroundColor: 'transparent',
                            color: 'var(--lx-fg3)',
                            cursor: isEdgeMutating ? 'not-allowed' : 'pointer',
                            display: 'flex', alignItems: 'center', justifyContent: 'center',
                            fontFamily: 'inherit', flexShrink: 0,
                            opacity: isEdgeMutating ? 0.4 : 1,
                          }}
                        >
                          <XIcon size={13} />
                        </button>
                      </li>
                    );
                  })
                )}
              </ul>
            </div>

            {/* Unlocks section */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                <span style={{ color: 'var(--lx-fg3)', fontSize: 13, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.04em' }}>
                  {strings.skillGraphUnlocksHeading}
                </span>
                {unlocksEdges.length > 0 && (
                  <span style={{
                    backgroundColor: 'var(--lx-card-soft)', borderRadius: 9999,
                    paddingLeft: 8, paddingRight: 8, height: 18, display: 'inline-flex',
                    alignItems: 'center', fontSize: 11, color: 'var(--lx-fg3)',
                  }}>
                    {unlocksEdges.length}
                  </span>
                )}
              </div>
              <ul
                role="list"
                aria-label={strings.skillGraphUnlocksAriaLabel.replace('{name}', selectedNode.name)}
                style={{ margin: 0, padding: 0, listStyle: 'none', display: 'flex', flexDirection: 'column', gap: 6 }}
              >
                {unlocksEdges.length === 0 ? (
                  <li data-testid="unlocks-empty"
                    style={{ fontSize: 13, color: 'var(--lx-fg3)', fontStyle: 'italic' }}>
                    {strings.skillGraphUnlocksEmpty}
                  </li>
                ) : (
                  unlocksEdges.map((edge) => {
                    const unlockNode = graph?.nodes.find((n) => n.id === edge.targetNodeId);
                    return (
                      <li key={edge.id} role="listitem"
                        data-testid={`unlock-item-${edge.id}`}
                        style={{
                          display: 'flex', alignItems: 'center', gap: 8,
                          padding: '8px 10px',
                          backgroundColor: 'rgba(255,255,255,0.03)',
                          borderRadius: 8, border: '1px solid var(--lx-border)',
                        }}
                      >
                        {unlockNode && <NodeTypePip nodeType={unlockNode.nodeType} />}
                        <span style={{ fontSize: 13, fontWeight: 500, color: 'var(--lx-fg2)', flex: 1 }}>
                          {unlockNode?.name ?? resolveNodeName(edge.targetNodeId)}
                        </span>
                      </li>
                    );
                  })
                )}
              </ul>
            </div>

            {/* Add prerequisite control */}
            <div style={{
              display: 'flex', flexDirection: 'column', gap: 8,
              paddingTop: 12, borderTop: '1px dashed rgba(255,255,255,0.06)',
            }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                <span style={{ color: 'var(--lx-primary)' }}><PlusCircleIcon size={14} /></span>
                <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--lx-primary)' }}>
                  {strings.skillGraphAddPrerequisiteHeading}
                </span>
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                <select
                  id="prerequisite-picker"
                  data-testid="prerequisite-picker"
                  aria-label={strings.skillGraphPickerPlaceholder}
                  value={pickerValue}
                  onChange={(e) => {
                    setPickerValue(e.target.value);
                    setEdgeErrorMsg(null);
                  }}
                  disabled={isEdgeMutating}
                  aria-disabled={isEdgeMutating}
                  style={{
                    flex: 1, height: 40, paddingLeft: 12, paddingRight: 12,
                    backgroundColor: 'var(--lx-bg)', borderRadius: 8,
                    border: '1px solid var(--lx-border)', color: 'var(--lx-fg2)',
                    fontSize: 14, fontFamily: 'inherit',
                    cursor: isEdgeMutating ? 'not-allowed' : 'pointer',
                    opacity: isEdgeMutating ? 0.6 : 1,
                    outline: 'none',
                  }}
                >
                  <option value="">
                    {pickerOptions.length === 0
                      ? strings.skillGraphPickerAllAdded
                      : strings.skillGraphPickerPlaceholder}
                  </option>
                  {pickerOptions.map((n) => (
                    <option key={n.id} value={n.id}>
                      {n.name}
                      {n.nodeType === NODE_TYPE.Concept ? ' [Concept]' : n.nodeType === NODE_TYPE.Review ? ' [Review]' : ''}
                    </option>
                  ))}
                </select>
                <button
                  data-testid="add-prerequisite-btn"
                  onClick={() => void handleAddEdge()}
                  disabled={!pickerValue || isEdgeMutating}
                  aria-busy={addEdgeMutation.isPending}
                  style={{
                    height: 40, paddingLeft: 16, paddingRight: 16,
                    borderRadius: 16, border: 'none',
                    backgroundColor: 'var(--lx-primary)', color: 'var(--lx-fg1)',
                    fontSize: 13, fontWeight: 600, cursor: (!pickerValue || isEdgeMutating) ? 'not-allowed' : 'pointer',
                    fontFamily: 'inherit', opacity: (!pickerValue || isEdgeMutating) ? 0.4 : 1,
                    display: 'flex', alignItems: 'center', gap: 6, flexShrink: 0,
                  }}
                >
                  {addEdgeMutation.isPending
                    ? <span style={{ width: 14, height: 14, borderRadius: 9999, border: '2px solid white', borderTopColor: 'transparent', animation: 'spin 0.8s linear infinite', display: 'inline-block' }} />
                    : <PlusIcon size={14} />}
                  {strings.skillGraphAddBtn}
                </button>
              </div>
            </div>

            {/* Skill detail section — only for Skill nodes with a skillId */}
            {selectedNode.nodeType === NODE_TYPE.Skill && selectedNode.skillId != null && (() => {
              const skillData = resolveSkill(selectedNode.skillId);
              if (!skillData) return null;
              return (
                <div style={{
                  paddingTop: 12, borderTop: '1px solid var(--lx-border)',
                  display: 'flex', flexDirection: 'column', gap: 8,
                }}>
                  <span style={{
                    fontSize: 13, fontWeight: 600, color: 'var(--lx-fg3)',
                    textTransform: 'uppercase', letterSpacing: '0.04em',
                  }}>
                    {strings.skillDetailSection}
                  </span>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                    <div style={{ display: 'flex', gap: 8, alignItems: 'baseline' }}>
                      <span style={{ fontSize: 12, color: 'var(--lx-fg3)', minWidth: 80 }}>{strings.skillDetailMastery}</span>
                      <span style={{ fontSize: 13, color: 'var(--lx-fg2)', fontVariantNumeric: 'tabular-nums' }} dir="ltr">
                        {skillData.masteryThreshold}%
                      </span>
                    </div>
                    <div style={{ display: 'flex', gap: 8, alignItems: 'baseline' }}>
                      <span style={{ fontSize: 12, color: 'var(--lx-fg3)', minWidth: 80 }}>{strings.skillDetailTime}</span>
                      <span style={{ fontSize: 13, color: 'var(--lx-fg2)', fontVariantNumeric: 'tabular-nums' }} dir="ltr">
                        {skillData.estimatedTimeMinutes} min
                      </span>
                    </div>
                  </div>
                  {onEditSkill && (
                    <button
                      data-testid={`skill-detail-edit-${skillData.id}`}
                      onClick={() => onEditSkill(skillData)}
                      style={{
                        border: 'none', backgroundColor: 'transparent',
                        color: 'var(--lx-primary)', fontSize: 13, fontWeight: 500,
                        cursor: 'pointer', fontFamily: 'inherit', padding: 0,
                        display: 'flex', alignItems: 'center', gap: 6, alignSelf: 'flex-start',
                      }}
                    >
                      <PencilIcon size={13} />
                      {strings.skillDetailEditLink}
                    </button>
                  )}
                </div>
              );
            })()}
          </div>
        )}
      </div>
    </>
  );
}

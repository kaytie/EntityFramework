using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Internal;

namespace Microsoft.Data.Entity.Utilities
{
    public class Multigraph<TVertex, TEdge> : Graph<TVertex>
    {
        private readonly HashSet<TVertex> _verticies = new HashSet<TVertex>();
        private readonly HashSet<TEdge> _edges = new HashSet<TEdge>();
        private readonly Dictionary<TVertex, Dictionary<TVertex, List<TEdge>>> _successorMap = new Dictionary<TVertex, Dictionary<TVertex, List<TEdge>>>();

        public virtual IEnumerable<TEdge> Edges
        {
            get { return _edges; }
        }

        public virtual IEnumerable<TEdge> GetEdges([NotNull] TVertex from, [NotNull] TVertex to)
        {
            Dictionary<TVertex, List<TEdge>> successorSet;
            if (_successorMap.TryGetValue(from, out successorSet))
            {
                List<TEdge> edgeList;
                if (successorSet.TryGetValue(to, out edgeList))
                {
                    return edgeList;
                }
            }
            return Enumerable.Empty<TEdge>();
        }

        public virtual void AddVertex([NotNull] TVertex vertex)
        {
            Check.NotNull(vertex, nameof(vertex));

            _verticies.Add(vertex);
        }

        public virtual void AddVertices([NotNull] IEnumerable<TVertex> verticies)
        {
            Check.NotNull(verticies, nameof(verticies));

            _verticies.UnionWith(verticies);
        }

        public virtual void AddEdge([NotNull] TVertex from, [NotNull] TVertex to, [NotNull] TEdge edge)
        {
            AddEdges(from, to, new[] { edge });
        }

        public virtual void AddEdges([NotNull] TVertex from, [NotNull] TVertex to, [NotNull] IEnumerable<TEdge> edges)
        {
            Check.NotNull(from, nameof(from));
            Check.NotNull(to, nameof(to));
            Check.NotNull(edges, nameof(edges));

            if (!_verticies.Contains(from))
            {
                throw new InvalidOperationException(Strings.GraphDoesNotContainVertex(from));
            }

            if (!_verticies.Contains(to))
            {
                throw new InvalidOperationException(Strings.GraphDoesNotContainVertex(to));
            }

            Dictionary<TVertex, List<TEdge>> successorSet;
            if (!_successorMap.TryGetValue(from, out successorSet))
            {
                successorSet = new Dictionary<TVertex, List<TEdge>>();
                _successorMap.Add(from, successorSet);
            }

            List<TEdge> edgeList;
            if (!successorSet.TryGetValue(to, out edgeList))
            {
                edgeList = new List<TEdge>();
                successorSet.Add(to, edgeList);
            }

            edgeList.AddRange(edges);
            _edges.UnionWith(edges);
        }

        public virtual IEnumerable<TVertex> TopologicalSort()
        {
            return TopologicalSort(null);
        }

        public virtual IEnumerable<TVertex> TopologicalSort([CanBeNull]Func<TVertex, TVertex, IEnumerable<TEdge>, bool> canBreakEdge)
        {
            var sortedQueue = new List<TVertex>();
            var predecessorCounts = new Dictionary<TVertex, int>();

            foreach (var vertex in _verticies)
            {
                var count = GetIncomingNeighbours(vertex).Count();
                if (count == 0)
                {
                    // Collect verticies without predecessors
                    sortedQueue.Add(vertex);
                }
                else
                {
                    // Track number of predecessors for remaining verticies
                    predecessorCounts[vertex] = count;
                }
            }

            var index = 0;
            while (sortedQueue.Count < _verticies.Count)
            {
                while (index < sortedQueue.Count)
                {
                    var currentRoot = sortedQueue[index];

                    foreach (var successor in GetOutgoingNeighbours(currentRoot).Where(neighbour => predecessorCounts.ContainsKey(neighbour)))
                    {
                        // Decrement counts for edges from sorted verticies and append any verticies that no longer have predecessors
                        predecessorCounts[successor]--;
                        if (predecessorCounts[successor] == 0)
                        {
                            sortedQueue.Add(successor);
                            predecessorCounts.Remove(successor);
                        }
                    }
                    index++;
                }

                // Cycle breaking
                if (sortedQueue.Count < _verticies.Count)
                {
                    var broken = false;

                    var candidateVertices = predecessorCounts.Keys.ToList();
                    var candidateIndex = 0;

                    // Iterrate over the unsorted verticies
                    while (candidateIndex < candidateVertices.Count && !broken && canBreakEdge != null)
                    {
                        var candidateVertex = candidateVertices[candidateIndex];

                        // Find verticies in the unsorted portion of the graph that have edges to the candidate
                        var incommingNeighbours = GetIncomingNeighbours(candidateVertex)
                            .Where(neighbour => predecessorCounts.ContainsKey(neighbour)).ToList();
                        var neighbourIndex = 0;

                        while (neighbourIndex < incommingNeighbours.Count && !broken)
                        {
                            var incommingNeighbour = incommingNeighbours[neighbourIndex];
                            // Check to see if the edge can be broken
                            if (canBreakEdge(incommingNeighbour, candidateVertex, _successorMap[incommingNeighbour][candidateVertex]))
                            {
                                predecessorCounts[candidateVertex]--;
                                if (predecessorCounts[candidateVertex] == 0)
                                {
                                    sortedQueue.Add(candidateVertex);
                                    predecessorCounts.Remove(candidateVertex);
                                    broken = true;
                                }
                            }
                            neighbourIndex++;
                        }
                        candidateIndex++;
                    }
                    if (!broken)
                    {
                        throw new InvalidOperationException(Strings.CycleBreakFailed());
                    }
                }
            }
            return sortedQueue;
        }

        public override IEnumerable<TVertex> Vertices
        {
            get { return _verticies; }
        }

        public override IEnumerable<TVertex> GetOutgoingNeighbours([NotNull]TVertex from)
        {
            Dictionary<TVertex, List<TEdge>> successorSet;
            if (_successorMap.TryGetValue(from, out successorSet))
            {
                return successorSet.Keys;
            }
            return Enumerable.Empty<TVertex>();
        }

        public override IEnumerable<TVertex> GetIncomingNeighbours([NotNull]TVertex to)
        {
            return _successorMap.Where(kvp => kvp.Value.ContainsKey(to)).Select(kvp => kvp.Key);
        }
    }
}

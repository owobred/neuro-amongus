﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Il2CppInterop.Runtime;
using Neuro.Pathfinding.DataStructures;
using Neuro.Utilities;
using UnityEngine;

namespace Neuro.Pathfinding;

// TODO: [BUG] Sometimes when very close to an object, the distance will be returned as -1
public sealed class PathfindingThread
{
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly ConcurrentDictionary<string, (Vector2 start, Vector2 target)> _requests = new();
    private readonly ConcurrentDictionary<string, (Vector2 start, Vector2 target, Vector2[] path, float length)> _results = new();

    private readonly Node[,] _grid;
    private readonly Thread _thread;

    public PathfindingThread(Node[,] grid, Vector2 accessiblePosition)
    {
        _grid = grid;
        FloodFill(accessiblePosition);

        _thread = new Thread(RunThread);
    }

    public void Start()
    {
        if (!_thread.IsAlive)
        {
            _thread.Start();
        }
    }

    public void Stop()
    {
        if (_thread.IsAlive)
        {
            _thread.Interrupt();
        }
    }

    public void RequestPath(Vector2 start, Vector2 target, string identifier)
    {
        if (_results.TryGetValue(identifier, out (Vector2 start, Vector2 target, Vector2[], float) result) &&
            result.start == start && result.target == target)
            return;

        if (!_queue.Contains(identifier)) _queue.Enqueue(identifier);

        _requests[identifier] = (start, target);
    }

    public bool TryGetPath(string identifier, out Vector2[] path, out float length)
    {
        bool tried = _results.TryGetValue(identifier, out (Vector2, Vector2, Vector2[] path, float length) result);
        path = result.path;
        length = result.length;
        return tried;
    }

    private void RunThread()
    {
        IL2CPP.il2cpp_thread_attach(IL2CPP.il2cpp_domain_get());

        while (true)
        {
            try
            {
                while (!_queue.IsEmpty)
                {
                    if (!_queue.TryDequeue(out string identifier)) continue;
                    if (!_requests.TryGetValue(identifier, out (Vector2 start, Vector2 target) vec)) continue;
                    _requests.TryRemove(identifier, out _);

                    Vector2[] path = FindPath(vec.start, vec.target);
                    _results[identifier] = (vec.start, vec.target, path, CalculateLength(path));

                    Thread.Yield();
                }

                Thread.Sleep(250);
            }
            catch (ThreadInterruptedException)
            {
                return;
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e);
                try
                {
                    Thread.Yield();
                }
                catch
                {
                    return;
                }
            }
        }
    }

    private void FloodFill(Vector2 accessiblePosition)
    {
        Node startingNode = NodeFromWorldPoint(accessiblePosition);

        List<Node> openSet = new();
        HashSet<Node> closedSet = new();
        openSet.Add(startingNode);

        while (openSet.Count > 0)
        {
            Node node = openSet[0];
            openSet.Remove(node);
            closedSet.Add(node);

            foreach (Node neighbour in GetNeighbours(node))
            {
                if (!neighbour.accessible || closedSet.Contains(neighbour)) continue;
                if (!openSet.Contains(neighbour)) openSet.Add(neighbour);

                neighbour.parent = node;
            }
        }

        foreach (Node node in closedSet.ToList())
        {
            CreateNodeVisualPoint(node.worldPosition);
        }

        // Set all nodes not in closed set to inaccessible
        foreach (Node node in _grid)
        {
            if (!closedSet.Contains(node)) node.accessible = false;
        }
    }

    private Vector2[] FindPath(Vector2 start, Vector2 target)
    {
        bool pathSuccess = false;

        Node startNode = FindClosestNode(start);
        Node targetNode = FindClosestNode(target);


        if (startNode is not { accessible: true } || targetNode is not { accessible: true }) return Array.Empty<Vector2>();

        Heap<Node> openSet = new(PathfindingHandler.GRID_SIZE * PathfindingHandler.GRID_SIZE);
        HashSet<Node> closedSet = new();

        openSet.Add(startNode);

        while (openSet.Count > 0)
        {
            Node currentNode = openSet.RemoveFirst();

            closedSet.Add(currentNode);

            if (currentNode == targetNode)
            {
                pathSuccess = true;
                break;
            }

            foreach (Node neighbour in GetNeighbours(currentNode))
            {
                if (!neighbour.accessible || closedSet.Contains(neighbour)) continue;

                int newMovementCostToNeighbour = currentNode.gCost + GetDistanceCost(currentNode, neighbour);

                if (newMovementCostToNeighbour >= neighbour.gCost && openSet.Contains(neighbour)) continue;

                neighbour.gCost = newMovementCostToNeighbour;
                neighbour.hCost = GetDistanceCost(neighbour, targetNode);
                neighbour.parent = currentNode;

                if (!openSet.Contains(neighbour))
                    openSet.Add(neighbour);
                else
                    openSet.UpdateItem(neighbour);
            }
        }

        if (pathSuccess)
        {
            return RetracePath(startNode, targetNode);
        }

        Warning("Failed to find path");
        return Array.Empty<Vector2>();
    }

    private float CalculateLength(Vector2[] path)
    {
        if (path.Length == 0) return -1;

        float length = 0f;
        for (int i = 0; i < path.Length - 1; i++)
        {
            length += Vector2.Distance(path[i], path[i + 1]);
        }

        return length;
    }

    private Node FindClosestNode(Vector2 position)
    {
        Node closestNode = NodeFromWorldPoint(position);

        Queue<Node> queue = new();
        List<Node> nodes = new();
        queue.Enqueue(closestNode);
        nodes.Add(closestNode);

        while (!closestNode.accessible)
        {
            closestNode = queue.Dequeue();
            float closestDistance = float.PositiveInfinity;
            Node closestNeighbour = null;
            foreach (Node neighbour in GetNeighbours(closestNode))
            {
                if (neighbour.accessible)
                {
                    float distance = Vector2.Distance(position, neighbour.worldPosition);
                    if (distance < closestDistance)
                    {
                        closestNeighbour = neighbour;
                        closestDistance = distance;
                    }
                }
                else
                {
                    if (!nodes.Contains(neighbour))
                    {
                        queue.Enqueue(neighbour);
                        nodes.Add(neighbour);
                    }
                }
            }

            if (closestNeighbour != null) closestNode = closestNeighbour;
        }

        return closestNode;
    }

    private Node NodeFromWorldPoint(Vector2 position)
    {
        position *= PathfindingHandler.GRID_DENSITY;
        float clampedX = Math.Clamp(position.x, PathfindingHandler.GRID_LOWER_BOUNDS, PathfindingHandler.GRID_UPPER_BOUNDS);
        float clampedY = Math.Clamp(position.y, PathfindingHandler.GRID_LOWER_BOUNDS, PathfindingHandler.GRID_UPPER_BOUNDS);

        int xIndex = (int) Math.Round(clampedX + PathfindingHandler.GRID_UPPER_BOUNDS);
        int yIndex = (int) Math.Round(clampedY + PathfindingHandler.GRID_UPPER_BOUNDS);

        return _grid[xIndex, yIndex];
    }

    private List<Node> GetNeighbours(Node node)
    {
        List<Node> neighbours = new();

        for (int x = -1; x <= 1; x++)
        for (int y = -1; y <= 1; y++)
        {
            if (x == 0 && y == 0)
                continue;

            int checkX = node.gridX + x;
            int checkY = node.gridY + y;

            if (checkX is >= 0 and < PathfindingHandler.GRID_SIZE && checkY is >= 0 and < PathfindingHandler.GRID_SIZE) neighbours.Add(_grid[checkX, checkY]);
        }

        return neighbours;
    }

    private static Vector2[] RetracePath(Node startNode, Node endNode)
    {
        List<Node> path = new();
        Node currentNode = endNode;
        while (currentNode != startNode)
        {
            path.Add(currentNode);
            currentNode = currentNode.parent;
        }

        Vector2[] waypoints = path.Select(p => p.worldPosition).ToArray();
        new Span<Vector2>(waypoints).Reverse();

        return waypoints;
    }

    private static int GetDistanceCost(Node a, Node b)
    {
        int dstX = Math.Abs(a.gridX - b.gridX);
        int dstY = Math.Abs(a.gridY - b.gridY);

        return 14 * dstY + 10 * Math.Abs(dstX - dstY);
    }

    private static void CreateNodeVisualPoint(Vector2 position) => CreateVisualPoint(position, Color.red, 0.1f);

    private static void CreateVisualPoint(Vector2 position, Color color, float widthMultiplier)
    {
        GameObject nodeVisualPoint = new("Gizmo (Visual Point)");
        nodeVisualPoint.transform.position = position;

        LineRenderer renderer = nodeVisualPoint.AddComponent<LineRenderer>();
        renderer.SetPosition(0, position);
        renderer.SetPosition(1, position + new Vector2(0, widthMultiplier));
        renderer.widthMultiplier = widthMultiplier;
        renderer.positionCount = 2;
        renderer.material = NeuroUtilities.MaskShaderMat;
        renderer.startColor = color;
        renderer.endColor = color;
    }
}

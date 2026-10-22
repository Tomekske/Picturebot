using System.Collections.Generic;
using CommunityToolkit.Mvvm.Messaging;
using Database.Domain.Entities;
using Picturebot.Messages;

namespace Picturebot.Services;

/// <summary>
///     Hierarchy navigation. The contract is documented on <see cref="INavigationService" /> and pinned by NavigationServiceTests.
/// </summary>
public class NavigationService : INavigationService, IRecipient<NodeSelectedMessage>, IRecipient<NodeDeletedMessage> {
    // Children we went back up from, most recent last: Forward walks back down this path.
    private readonly Stack<Node> _forward = new();

    // Nodes seen so far by id. A node's Parent isn't always loaded (a freshly created or imported node only has
    // ParentId), and going up must not fall back to the library root because of that.
    private readonly Dictionary<int, Node> _known = new();
    private Node? _currentNode;
    private bool _isNavigatingInternally;

    public NavigationService() {
        WeakReferenceMessenger.Default.Register<NodeSelectedMessage>(this);
        WeakReferenceMessenger.Default.Register<NodeDeletedMessage>(this);
    }

    public Node? CurrentNode => _currentNode;

    public bool CanGoBack => _currentNode != null;

    public bool CanGoForward => _forward.Count > 0;

    public void Receive(NodeSelectedMessage message) {
        if (_isNavigatingInternally) return;

        var node = message.Value;
        if (_currentNode == node) return;

        // Any navigation that isn't Back/Forward starts a new path.
        _forward.Clear();
        SetCurrent(node);
    }

    public void Receive(NodeDeletedMessage message) {
        var deletedNode = message.Value;
        if (deletedNode == null) return;

        _known.Remove(deletedNode.Id);
        var remaining = new Stack<Node>();
        foreach (var node in _forward) {
            if (node.Id != deletedNode.Id && !IsInside(node, deletedNode.Id)) remaining.Push(node);
        }
        _forward.Clear();
        foreach (var node in remaining) _forward.Push(node);

        if (_currentNode != null && (_currentNode.Id == deletedNode.Id || IsInside(_currentNode, deletedNode.Id))) {
            NavigateTo(ParentOf(deletedNode));
        }
    }

    public void NavigateTo(Node? node, bool addToHistory = true) {
        if (_currentNode == node) return;

        if (addToHistory) {
            _forward.Clear();
        }
        Select(node);
    }

    public void GoBack() {
        if (_currentNode == null) return;

        var child = _currentNode;
        Select(ParentOf(child));
        _forward.Push(child);
    }

    public void GoForward() {
        if (_forward.Count == 0) return;

        Select(_forward.Pop());
    }

    public void GoUp() => GoBack();

    private void Select(Node? node) {
        SetCurrent(node);
        _isNavigatingInternally = true;
        try {
            WeakReferenceMessenger.Default.Send(new NodeSelectedMessage(node));
        } finally {
            _isNavigatingInternally = false;
        }
    }

    private void SetCurrent(Node? node) {
        _currentNode = node;
        for (var known = node; known != null; known = known.Parent) {
            _known[known.Id] = known;
        }
    }

    private Node? ParentOf(Node node) {
        if (node.Parent != null) return node.Parent;
        return node.ParentId.HasValue && _known.TryGetValue(node.ParentId.Value, out var parent) ? parent : null;
    }

    private bool IsInside(Node node, int ancestorId) {
        for (var parent = ParentOf(node); parent != null; parent = ParentOf(parent)) {
            if (parent.Id == ancestorId) return true;
        }
        return false;
    }
}

using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Messaging;
using Database.Domain.Entities;
using Picturebot.Messages;

namespace Picturebot.Services;

public class NavigationService : INavigationService {
    private readonly List<Node?> _history = new();
    private int _historyIndex = -1;
    private Node? _currentNode;

    public Node? CurrentNode => _currentNode;

    public bool CanGoBack => _currentNode?.Parent != null || (_currentNode == null && _history.Count > 0 && _history.Any(n => n != null));
    
    public bool CanGoForward {
        get {
            // Can go forward if there is a descendant in the history
            return _history.Any(node => IsDescendantOf(node, _currentNode));
        }
    }

    public void NavigateTo(Node? node, bool addToHistory = true) {
        if (_currentNode == node) return;

        if (addToHistory) {
            // When navigating to a new node, we want to update our "lineage"
            if (_historyIndex >= 0 && _historyIndex < _history.Count - 1) {
                // Clear future history if we deviate
                _history.RemoveRange(_historyIndex + 1, _history.Count - (_historyIndex + 1));
            }
            
            _history.Add(node);
            _historyIndex = _history.Count - 1;
        }

        _currentNode = node;
        WeakReferenceMessenger.Default.Send(new NodeSelectedMessage(node!));
    }

    public void GoBack() {
        if (_currentNode != null) {
            // Structural Back: Go to Parent
            _currentNode = _currentNode.Parent;
            WeakReferenceMessenger.Default.Send(new NodeSelectedMessage(_currentNode!));
        }
    }

    public void GoForward() {
        // Find the most recently visited descendant of the current node
        for (int i = _history.Count - 1; i >= 0; i--) {
            var target = _history[i];
            if (IsDescendantOf(target, _currentNode)) {
                var nextNode = GetNextStepTowards(target, _currentNode);
                if (nextNode != null) {
                    _currentNode = nextNode;
                    WeakReferenceMessenger.Default.Send(new NodeSelectedMessage(_currentNode!));
                    return;
                }
            }
        }
    }

    private bool IsDescendantOf(Node? potentialDescendant, Node? potentialAncestor) {
        if (potentialDescendant == null) return false;
        if (potentialAncestor == null) return true; // Everything is a descendant of the root (null)
        if (potentialDescendant.Id == potentialAncestor.Id) return false; // Not a descendant of itself

        var current = potentialDescendant.Parent;
        while (current != null) {
            if (current.Id == potentialAncestor.Id) return true;
            current = current.Parent;
        }
        return false;
    }

    private Node? GetNextStepTowards(Node? target, Node? current) {
        if (target == null || target == current) return null;
        
        var path = new List<Node>();
        var temp = target;
        
        // Build path from target up to current
        while (temp != null && (current == null || temp.Id != current.Id)) {
            path.Insert(0, temp);
            temp = temp.Parent;
        }
        
        // If we found the current node (or current was null and we hit the root), 
        // the first element in the path is the next step down.
        return path.FirstOrDefault();
    }

    public void GoUp() {
        GoBack();
    }
}

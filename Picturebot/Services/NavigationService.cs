using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Messaging;
using Database.Domain.Entities;
using Picturebot.Messages;

namespace Picturebot.Services;

public class NavigationService : INavigationService, IRecipient<NodeSelectedMessage> {
    private readonly List<Node?> _history = new() { null };
    private int _historyIndex = 0;
    private Node? _currentNode;
    private bool _isNavigatingInternally;

    public NavigationService() {
        WeakReferenceMessenger.Default.Register<NodeSelectedMessage>(this);
    }

    public Node? CurrentNode => _currentNode;

    public bool CanGoBack => _historyIndex > 0 || _currentNode != null;
    
    public bool CanGoForward => _historyIndex >= 0 && _historyIndex < _history.Count - 1;

    public void Receive(NodeSelectedMessage message) {
        if (_isNavigatingInternally) return;

        var node = message.Value;
        if (_currentNode == node) return;

        if (_historyIndex >= 0 && _historyIndex < _history.Count - 1) {
            _history.RemoveRange(_historyIndex + 1, _history.Count - (_historyIndex + 1));
        }

        _history.Add(node);
        _historyIndex = _history.Count - 1;
        _currentNode = node;
    }

    public void NavigateTo(Node? node, bool addToHistory = true) {
        if (_currentNode == node) return;

        if (addToHistory) {
            if (_historyIndex >= 0 && _historyIndex < _history.Count - 1) {
                _history.RemoveRange(_historyIndex + 1, _history.Count - (_historyIndex + 1));
            }
            
            _history.Add(node);
            _historyIndex = _history.Count - 1;
        }

        _currentNode = node;
        _isNavigatingInternally = true;
        try {
            WeakReferenceMessenger.Default.Send(new NodeSelectedMessage(node));
        } finally {
            _isNavigatingInternally = false;
        }
    }

    public void GoBack() {
        if (_historyIndex > 0) {
            _historyIndex--;
            _currentNode = _history[_historyIndex];
            _isNavigatingInternally = true;
            try {
                WeakReferenceMessenger.Default.Send(new NodeSelectedMessage(_currentNode));
            } finally {
                _isNavigatingInternally = false;
            }
            return;
        }

        if (_currentNode != null) {
            // Structural fallback: go to parent or library root (null)
            NavigateTo(_currentNode.Parent);
        }
    }

    public void GoForward() {
        if (_historyIndex >= 0 && _historyIndex < _history.Count - 1) {
            _historyIndex++;
            _currentNode = _history[_historyIndex];
            _isNavigatingInternally = true;
            try {
                WeakReferenceMessenger.Default.Send(new NodeSelectedMessage(_currentNode));
            } finally {
                _isNavigatingInternally = false;
            }
        }
    }

    public void GoUp() {
        if (_currentNode != null) {
            NavigateTo(_currentNode.Parent);
        } else {
            GoBack();
        }
    }
}

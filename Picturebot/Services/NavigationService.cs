using System.Collections.Generic;
using CommunityToolkit.Mvvm.Messaging;
using Database.Domain.Entities;
using Picturebot.Messages;

namespace Picturebot.Services;

public class NavigationService : INavigationService {
    private readonly Stack<Node?> _backStack = new();
    private readonly Stack<Node?> _forwardStack = new();
    private Node? _currentNode;

    public Node? CurrentNode => _currentNode;

    public bool CanGoBack => _backStack.Count > 0;
    public bool CanGoForward => _forwardStack.Count > 0;

    public void NavigateTo(Node? node, bool addToHistory = true) {
        if (_currentNode == node && _backStack.Count > 0) return; 

        if (addToHistory && _currentNode != node) {
            _backStack.Push(_currentNode);
            _forwardStack.Clear();
        }

        _currentNode = node;
        WeakReferenceMessenger.Default.Send(new NodeSelectedMessage(node!));
    }

    public void GoBack() {
        if (CanGoBack) {
            _forwardStack.Push(_currentNode);
            _currentNode = _backStack.Pop();
            WeakReferenceMessenger.Default.Send(new NodeSelectedMessage(_currentNode!));
        } else {
            // If no history, navigate one level up (Expected Behavior)
            GoUp();
        }
    }

    public void GoForward() {
        if (CanGoForward) {
            _backStack.Push(_currentNode);
            _currentNode = _forwardStack.Pop();
            WeakReferenceMessenger.Default.Send(new NodeSelectedMessage(_currentNode!));
        }
    }

    public void GoUp() {
        if (_currentNode != null) {
            NavigateTo(_currentNode.Parent);
        }
    }
}

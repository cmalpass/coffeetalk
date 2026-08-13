window.scrollToBottom = (elementId) => {
    const element = document.getElementById(elementId);
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
};

window.renderMermaid = async () => {
    if (!window.mermaid) {
        return;
    }

    window.mermaid.initialize({ startOnLoad: false, securityLevel: 'strict', theme: 'default' });
    const diagrams = document.querySelectorAll('.mermaid:not([data-processed="true"])');
    if (diagrams.length > 0) {
        await window.mermaid.run({ nodes: Array.from(diagrams) });
    }
};

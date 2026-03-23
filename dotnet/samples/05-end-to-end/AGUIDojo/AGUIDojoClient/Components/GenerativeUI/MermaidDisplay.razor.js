let mermaidPromise;

async function getMermaid() {
    if (!mermaidPromise) {
        mermaidPromise = import("https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.esm.min.mjs")
            .then(module => {
                const mermaid = module.default;
                mermaid.initialize({
                    startOnLoad: false,
                    securityLevel: "strict",
                    theme: "default"
                });

                return mermaid;
            });
    }

    return mermaidPromise;
}

export async function renderDiagram(hostElement, definition, elementId) {
    if (!hostElement || !definition) {
        return;
    }

    const mermaid = await getMermaid();
    const { svg, bindFunctions } = await mermaid.render(elementId, definition);

    hostElement.innerHTML = svg;
    if (bindFunctions) {
        bindFunctions(hostElement);
    }
}

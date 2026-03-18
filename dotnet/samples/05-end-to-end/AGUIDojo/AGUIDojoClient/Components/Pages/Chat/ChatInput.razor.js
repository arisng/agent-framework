export function init(elem) {
    elem.focus();

    // Auto-resize whenever the user types or if the value is set programmatically
    elem.addEventListener('input', () => resizeToFit(elem));
    afterPropertyWritten(elem, 'value', () => resizeToFit(elem));

    // Auto-submit the form on 'enter' keypress
    elem.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            elem.dispatchEvent(new CustomEvent('change', { bubbles: true }));
            elem.closest('form').dispatchEvent(new CustomEvent('submit', { bubbles: true, cancelable: true }));
        }
    });
}

/**
 * Triggers the hidden file input element.
 */
export function triggerFilePicker(fileInput) {
    fileInput.click();
}

/**
 * Initializes attachment support: file input change, drag-and-drop, and clipboard paste.
 * @param {HTMLElement} container - The input-box container element.
 * @param {HTMLInputElement} fileInput - The hidden file input element.
 * @param {object} dotNetRef - The .NET object reference for JS interop callbacks.
 */
export function initAttachments(container, fileInput, dotNetRef) {
    // File input change handler
    fileInput.addEventListener('change', () => {
        if (fileInput.files && fileInput.files.length > 0) {
            uploadFiles(Array.from(fileInput.files), dotNetRef);
            fileInput.value = '';
        }
    });

    // Drag-and-drop handlers
    container.addEventListener('dragover', (e) => {
        e.preventDefault();
        e.stopPropagation();
        container.classList.add('drag-over');
    });

    container.addEventListener('dragleave', (e) => {
        e.preventDefault();
        e.stopPropagation();
        container.classList.remove('drag-over');
    });

    container.addEventListener('drop', (e) => {
        e.preventDefault();
        e.stopPropagation();
        container.classList.remove('drag-over');

        const files = Array.from(e.dataTransfer.files).filter(f => f.type.startsWith('image/'));
        if (files.length > 0) {
            uploadFiles(files, dotNetRef);
        }
    });

    // Paste handler for images
    container.addEventListener('paste', (e) => {
        const items = Array.from(e.clipboardData?.items || []);
        const imageFiles = items
            .filter(item => item.kind === 'file' && item.type.startsWith('image/'))
            .map(item => item.getAsFile())
            .filter(Boolean);

        if (imageFiles.length > 0) {
            e.preventDefault();
            uploadFiles(imageFiles, dotNetRef);
        }
    });
}

const MAX_FILE_SIZE = 10 * 1024 * 1024; // 10 MB
const ALLOWED_TYPES = ['image/png', 'image/jpeg', 'image/gif', 'image/webp'];

/**
 * Uploads files to the server via fetch and notifies Blazor of the results.
 * @param {File[]} files - Array of File objects to upload.
 * @param {object} dotNetRef - The .NET object reference for callbacks.
 */
async function uploadFiles(files, dotNetRef) {
    if (files.length === 0) {
        return;
    }

    await dotNetRef.invokeMethodAsync('OnUploadBatchStarted', files.length);

    try {
    for (const file of files) {
        if (file.size > MAX_FILE_SIZE) {
            dotNetRef.invokeMethodAsync('OnFileUploadError', file.name, 'File too large (max 10 MB)');
            continue;
        }

        if (!ALLOWED_TYPES.includes(file.type)) {
            dotNetRef.invokeMethodAsync('OnFileUploadError', file.name, 'Unsupported file type');
            continue;
        }

        try {
            const formData = new FormData();
            formData.append('file', file);

            const response = await fetch('/api/files', {
                method: 'POST',
                body: formData,
            });

            if (!response.ok) {
                const errorText = await response.text();
                dotNetRef.invokeMethodAsync('OnFileUploadError', file.name, errorText);
                continue;
            }

            const result = await response.json();
            dotNetRef.invokeMethodAsync('OnFileUploaded', result.id, result.fileName, result.contentType, result.size);
        } catch (error) {
            dotNetRef.invokeMethodAsync('OnFileUploadError', file.name, error.message || 'Upload failed');
        }
    }
    } finally {
        await dotNetRef.invokeMethodAsync('OnUploadBatchFinished', files.length);
    }
}

function resizeToFit(elem) {
    const lineHeight = parseFloat(getComputedStyle(elem).lineHeight);

    elem.rows = 1;
    const numLines = Math.ceil(elem.scrollHeight / lineHeight);
    elem.rows = Math.min(5, Math.max(1, numLines));
}

function afterPropertyWritten(target, propName, callback) {
    const descriptor = getPropertyDescriptor(target, propName);
    Object.defineProperty(target, propName, {
        get: function () {
            return descriptor.get.apply(this, arguments);
        },
        set: function () {
            const result = descriptor.set.apply(this, arguments);
            callback();
            return result;
        }
    });
}

function getPropertyDescriptor(target, propertyName) {
    return Object.getOwnPropertyDescriptor(target, propertyName)
        || getPropertyDescriptor(Object.getPrototypeOf(target), propertyName);
}

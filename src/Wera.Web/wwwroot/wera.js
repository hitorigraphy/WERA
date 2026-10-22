window.wera = {
    download(filename, text) {
        const url = URL.createObjectURL(new Blob([text], { type: "application/json" }));
        const a = Object.assign(document.createElement("a"), { href: url, download: filename });
        a.click();
        URL.revokeObjectURL(url);
    },
};

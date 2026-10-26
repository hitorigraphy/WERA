window.wera = {
    /** 화면 영역의 스크롤을 맨 아래(최신 대사) 또는 맨 위로 옮긴다. */
    scroll(element, toBottom) {
        element.scrollTop = toBottom ? element.scrollHeight : 0;
    },
    download(filename, text) {
        const url = URL.createObjectURL(new Blob([text], { type: "application/json" }));
        const a = Object.assign(document.createElement("a"), { href: url, download: filename });
        a.click();
        URL.revokeObjectURL(url);
    },
};

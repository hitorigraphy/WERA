window.wera = {
    /** 화면 영역의 스크롤을 맨 아래(최신 대사) 또는 맨 위로 옮긴다. */
    scroll(element, toBottom) {
        element.scrollTop = toBottom ? element.scrollHeight : 0;
    },
    /** 입력칸의 글을 비운다(던전 이동은 키를 누르는 즉시 처리하고 글을 남기지 않는다). */
    clearInput(element) {
        element.value = "";
    },
    download(filename, text) {
        const url = URL.createObjectURL(new Blob([text], { type: "application/json" }));
        const a = Object.assign(document.createElement("a"), { href: url, download: filename });
        a.click();
        URL.revokeObjectURL(url);
    },
};

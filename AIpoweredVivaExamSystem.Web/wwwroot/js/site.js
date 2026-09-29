// Toast: auto-hide after a few seconds, or on click.
document.querySelectorAll("[data-toast]").forEach((toast) => {
    const hide = () => { toast.classList.add("hide"); setTimeout(() => toast.remove(), 200); };
    toast.querySelector("[data-toast-close]")?.addEventListener("click", hide);
    setTimeout(hide, 4500);
});

// Delete confirmation: any element with data-confirm-action opens the shared dialog,
// which posts (with the antiforgery token) to that action.
const dialog = document.getElementById("confirm-dialog");
if (dialog) {
    const form = dialog.querySelector("[data-confirm-form]");
    document.addEventListener("click", (event) => {
        const trigger = event.target.closest("[data-confirm-action]");
        if (!trigger) return;
        event.preventDefault();
        form.action = trigger.dataset.confirmAction;
        dialog.querySelector("[data-confirm-title]").textContent = trigger.dataset.confirmTitle || "Xóa mục này?";
        dialog.querySelector("[data-confirm-text]").textContent =
            trigger.dataset.confirmText || "Thao tác này không thể hoàn tác.";
        dialog.showModal();
    });
    dialog.querySelector("[data-confirm-cancel]").addEventListener("click", () => dialog.close());
    dialog.addEventListener("click", (event) => { if (event.target === dialog) dialog.close(); });
}

// Filters: submit the toolbar form as soon as a select changes.
document.querySelectorAll("[data-auto-submit]").forEach((select) =>
    select.addEventListener("change", () => select.form.submit()));

// Question form: reload the topic list when the subject changes.
const subjectSelect = document.querySelector("[data-subject-select]");
const topicSelect = document.querySelector("[data-topic-select]");
if (subjectSelect && topicSelect) {
    subjectSelect.addEventListener("change", async () => {
        const placeholder = topicSelect.options[0];
        topicSelect.replaceChildren(placeholder);
        topicSelect.value = "";
        if (!subjectSelect.value) return;
        const url = `${topicSelect.dataset.topicSource}?subjectId=${encodeURIComponent(subjectSelect.value)}`;
        const response = await fetch(url, { headers: { Accept: "application/json" } });
        if (!response.ok) return;
        for (const topic of await response.json())
            topicSelect.append(new Option(topic.name, topic.id));
    });
}

// Rubric form: add/remove criteria rows, keep indexes contiguous for model binding, show the live total.
const criteria = document.querySelector("[data-criteria]");
if (criteria) {
    const template = document.getElementById("criterion-template");
    const total = document.querySelector("[data-total]");
    const renumber = () => {
        const rows = criteria.querySelectorAll("[data-criterion]");
        rows.forEach((row, i) => {
            row.querySelector("[data-criterion-index]").textContent = i + 1;
            row.querySelectorAll("[name^='Criteria[']").forEach((input) =>
                input.name = input.name.replace(/^Criteria\[[^\]]+\]/, `Criteria[${i}]`));
            row.querySelector("[data-remove-criterion]").disabled = rows.length === 1;
        });
        const sum = [...criteria.querySelectorAll("[data-score]")]
            .reduce((acc, input) => acc + (parseFloat(input.value) || 0), 0);
        total.textContent = Math.round(sum * 100) / 100;
    };
    document.querySelector("[data-add-criterion]").addEventListener("click", () => {
        criteria.append(template.content.cloneNode(true));
        renumber();
        criteria.lastElementChild.querySelector("input").focus();
    });
    criteria.addEventListener("click", (event) => {
        const remove = event.target.closest("[data-remove-criterion]");
        if (!remove) return;
        remove.closest("[data-criterion]").remove();
        renumber();
    });
    criteria.addEventListener("input", (event) => { if (event.target.matches("[data-score]")) renumber(); });
    renumber();
}

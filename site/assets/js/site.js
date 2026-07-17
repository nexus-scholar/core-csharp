(function () {
  const toggle = document.querySelector(".nav-toggle");
  const links = document.querySelector(".nav-links");
  if (toggle && links) {
    toggle.addEventListener("click", function () {
      const expanded = toggle.getAttribute("aria-expanded") === "true";
      toggle.setAttribute("aria-expanded", String(!expanded));
      links.classList.toggle("is-open", !expanded);
    });
  }

  document.querySelectorAll("pre").forEach(function (pre) {
    const wrapper = document.createElement("div");
    wrapper.className = "code-block";
    pre.parentNode.insertBefore(wrapper, pre);
    wrapper.appendChild(pre);

    const button = document.createElement("button");
    button.className = "code-copy";
    button.type = "button";
    button.textContent = "Copy";
    button.setAttribute("aria-label", "Copy code block");
    button.addEventListener("click", function () {
      navigator.clipboard.writeText(pre.innerText).then(function () {
        button.textContent = "Copied";
        window.setTimeout(function () {
          button.textContent = "Copy";
        }, 1600);
      });
    });
    wrapper.appendChild(button);
  });

  const filter = document.querySelector("[data-module-filter]");
  const modules = Array.from(document.querySelectorAll("[data-module-card]"));
  const categories = Array.from(document.querySelectorAll(".module-category"));
  const empty = document.querySelector("[data-module-empty]");
  if (filter && modules.length) {
    filter.addEventListener("input", function () {
      const query = filter.value.trim().toLowerCase();
      let visible = 0;
      modules.forEach(function (module) {
        const matches = !query || module.textContent.toLowerCase().includes(query);
        module.hidden = !matches;
        if (matches) {
          visible += 1;
        }
      });
      categories.forEach(function (category) {
        const categoryModules = Array.from(category.querySelectorAll("[data-module-card]"));
        category.hidden = categoryModules.length > 0 && !categoryModules.some(function (module) {
          return !module.hidden;
        });
      });
      if (empty) {
        empty.hidden = visible !== 0;
        empty.classList.toggle("is-visible", visible === 0);
      }
    });
  }
})();

/*
 * Writes the proof-of-presence answer into the spam guard's hidden input.
 *
 * This is obfuscation, not cryptography. The algorithm ships in a public package, so a correct answer proves
 * only that a JavaScript engine ran on the page — which is still enough to exclude the many scrapers that fetch
 * and parse without executing scripts.
 *
 * Must stay in step with SpamGuardTokenService.ComputeJavaScriptAnswer.
 */
(function () {
  "use strict";

  function computeAnswer(nonce) {
    var reversed = nonce.split("").reverse().join("");
    return btoa(reversed).replace(/=+$/, "").replace(/\+/g, "-").replace(/\//g, "_");
  }

  function fill(root) {
    var inputs = root.querySelectorAll("input[data-ucfsg-nonce]");
    for (var i = 0; i < inputs.length; i++) {
      var input = inputs[i];
      var nonce = input.getAttribute("data-ucfsg-nonce");
      if (nonce) {
        input.value = computeAnswer(nonce);
      }
    }
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", function () {
      fill(document);
    });
  } else {
    fill(document);
  }
})();

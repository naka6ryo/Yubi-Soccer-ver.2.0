mergeInto(LibraryManager.library, {
  __registerEmbeddedReceiver: function (strPtr) {
    try {
      var name = UTF8ToString(strPtr);
      if (typeof window !== "undefined" && window.__registerEmbeddedReceiver) {
        try {
          window.__registerEmbeddedReceiver(name);
        } catch (e) {
          console.warn("[embeddedRelay.jslib] forward failed", e);
        }
      } else {
        // store as accepted name so page logic can pick it up even if register fn not defined
        if (typeof window !== "undefined") {
          window.__acceptedEmbeddedReceiverName = name;
        }
      }
    } catch (e) {
      console.warn("[embeddedRelay.jslib] __registerEmbeddedReceiver error", e);
    }
  },
});

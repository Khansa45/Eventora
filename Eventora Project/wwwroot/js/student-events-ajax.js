(function ($) {
    "use strict";

    function showMessage(success, message) {
        var alertClass = success ? "alert-success" : "alert-danger";
        var iconClass = success ? "bi-check-circle-fill" : "bi-exclamation-triangle-fill";
        var safeMessage = $("<div>").text(message || "").html();

        $("#ajaxMessage")
            .attr("class", "alert " + alertClass + " mt-3")
            .html('<i class="bi ' + iconClass + ' me-1"></i> ' + safeMessage);
    }

    function getAntiForgeryToken($form) {
        return $form.find('input[name="__RequestVerificationToken"]').val() ||
            $('input[name="__RequestVerificationToken"]').first().val() ||
            "";
    }

    $(document).on("click", ".js-view-details", function () {
        var eventId = $(this).data("event-id");
        var $body = $("#studentEventDetailsBody");
        var modalElement = document.getElementById("studentEventDetailsModal");

        $body.html('<div class="text-muted">Loading event details...</div>');
        bootstrap.Modal.getOrCreateInstance(modalElement).show();

        $.get("/Student/EventDetailsPartial", { id: eventId }, function (html) {
            $body.html(html);
        }, "html").fail(function (xhr) {
            var message = xhr.status === 404
                ? "This event is not available."
                : "Event details could not be loaded. Please try again.";
            $body.html('<div class="text-danger"><i class="bi bi-exclamation-triangle-fill me-1"></i> ' + message + "</div>");
        });
    });

    $(document).on("submit", 'form[data-ajax-register="true"]', function (event) {
        event.preventDefault();

        var $form = $(this);
        var $button = $form.find('button[type="submit"]');
        var originalButtonHtml = $button.html();
        var eventId = $form.find('input[name="eventId"]').val();
        var token = getAntiForgeryToken($form);

        $button.prop("disabled", true).html('<span class="spinner-border spinner-border-sm me-1"></span> Registering...');

        $.post("/Student/RegisterEvent", {
            eventId: eventId,
            __RequestVerificationToken: token
        }, function (response) {
            var success = !!(response && response.success);
            var message = response && response.message
                ? response.message
                : "Registration could not be completed. Please try again.";

            showMessage(success, message);

            if (success) {
                $form.replaceWith('<button type="button" class="btn-registered" disabled><i class="bi bi-check-circle-fill me-1"></i> Already Registered</button>');
            } else {
                $button.prop("disabled", false).html(originalButtonHtml);
            }
        }, "json").fail(function (xhr) {
            var message = xhr.status === 400
                ? "The registration request could not be verified. Please refresh the page and try again."
                : "Registration could not be completed. Please try again.";

            showMessage(false, message);
            $button.prop("disabled", false).html(originalButtonHtml);
        });
    });

    $(document).on("submit", "#eventFilterForm", function (event) {
        event.preventDefault();

        var $form = $(this);
        var search = $form.find('input[name="search"]').val() || "";
        var categoryId = $form.find('select[name="categoryId"]').val() || "0";

        $.ajax({
            url: "/Student/EventListPartial",
            type: "GET",
            data: {
                search: search,
                categoryId: categoryId
            },
            dataType: "html",
            success: function (html) {
                $("#approvedEventsContainer").html(html);
                var count = $("#approvedEventsContainer > .col-md-4").length;
                $("#approvedEventsCount").text("Showing " + count + " approved event(s)");
            },
            error: function () {
                showMessage(false, "Events could not be filtered. Please try again.");
            }
        });
    });
})(jQuery);

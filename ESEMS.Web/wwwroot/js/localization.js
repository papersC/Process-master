/**
 * JavaScript Localization Helper for ESEMS
 * Provides client-side access to server-side resource strings
 */

// Global localization object - populated from server
window.ESEMS = window.ESEMS || {};
window.ESEMS.Localization = window.ESEMS.Localization || {};

/**
 * Get localized string by key
 * @param {string} key - Resource key
 * @param {string} defaultValue - Default value if key not found
 * @returns {string} Localized string
 */
window.ESEMS.Localization.get = function(key, defaultValue) {
    return window.ESEMS.Localization[key] || defaultValue || key;
};

/**
 * Get DataTables localization object
 * @returns {object} DataTables language configuration
 */
window.ESEMS.Localization.getDataTablesConfig = function() {
    return {
        search: window.ESEMS.Localization.get('DataTables_Search', 'Search:'),
        lengthMenu: window.ESEMS.Localization.get('DataTables_LengthMenu', 'Show _MENU_ entries'),
        info: window.ESEMS.Localization.get('DataTables_Info', 'Showing _START_ to _END_ of _TOTAL_ entries'),
        infoEmpty: window.ESEMS.Localization.get('DataTables_InfoEmpty', 'Showing 0 to 0 of 0 entries'),
        infoFiltered: window.ESEMS.Localization.get('DataTables_InfoFiltered', '(filtered from _MAX_ total entries)'),
        zeroRecords: window.ESEMS.Localization.get('DataTables_ZeroRecords', 'No matching records found'),
        emptyTable: window.ESEMS.Localization.get('DataTables_EmptyTable', 'No data available in table'),
        paginate: {
            first: window.ESEMS.Localization.get('DataTables_Paginate_First', 'First'),
            last: window.ESEMS.Localization.get('DataTables_Paginate_Last', 'Last'),
            next: window.ESEMS.Localization.get('DataTables_Paginate_Next', 'Next'),
            previous: window.ESEMS.Localization.get('DataTables_Paginate_Previous', 'Previous')
        }
    };
};

/**
 * Get jQuery Validation localization
 * @returns {object} Validation messages
 */
window.ESEMS.Localization.getValidationMessages = function() {
    return {
        required: window.ESEMS.Localization.get('Validation_Required', 'This field is required.'),
        email: window.ESEMS.Localization.get('Validation_Email', 'Please enter a valid email address.'),
        url: window.ESEMS.Localization.get('Validation_Url', 'Please enter a valid URL.'),
        date: window.ESEMS.Localization.get('Validation_Date', 'Please enter a valid date.'),
        number: window.ESEMS.Localization.get('Validation_Number', 'Please enter a valid number.'),
        digits: window.ESEMS.Localization.get('Validation_Digits', 'Please enter only digits.'),
        minlength: window.ESEMS.Localization.get('Validation_MinLength', 'Please enter at least {0} characters.'),
        maxlength: window.ESEMS.Localization.get('Validation_MaxLength', 'Please enter no more than {0} characters.'),
        range: window.ESEMS.Localization.get('Validation_Range', 'Please enter a value between {0} and {1}.')
    };
};

/**
 * Get SweetAlert2 default configuration
 * @returns {object} SweetAlert2 configuration
 */
window.ESEMS.Localization.getSweetAlertDefaults = function() {
    return {
        confirmButtonText: window.ESEMS.Localization.get('Confirm_YesDelete', 'Yes, delete it!'),
        cancelButtonText: window.ESEMS.Localization.get('Button_Cancel', 'Cancel'),
        confirmButtonColor: '#005B99',
        cancelButtonColor: '#6b7280'
    };
};

/**
 * Initialize localization for common components
 */
window.ESEMS.Localization.init = function() {
    // Apply DataTables localization to all tables with .datatable class
    if (typeof $.fn.dataTable !== 'undefined') {
        $.extend(true, $.fn.dataTable.defaults, {
            language: window.ESEMS.Localization.getDataTablesConfig()
        });
    }

    // Apply jQuery Validation localization
    if (typeof $.validator !== 'undefined') {
        $.extend($.validator.messages, window.ESEMS.Localization.getValidationMessages());
    }

    // Set SweetAlert2 defaults
    if (typeof Swal !== 'undefined') {
        Swal.mixin(window.ESEMS.Localization.getSweetAlertDefaults());
    }
};

// Auto-initialize when DOM is ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function() {
        window.ESEMS.Localization.init();
    });
} else {
    window.ESEMS.Localization.init();
}


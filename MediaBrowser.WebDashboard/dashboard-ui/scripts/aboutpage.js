﻿(function ($, document) {

    $(document).on('pageshow', "#aboutPage", function () {

        var page = this;
        
        ApiClient.getSystemInfo().done(function (info) {

            var elem = $('#appVersionNumber', page);
            
            elem.html(elem.html().replace('{0}', info.Version));
        });
    });

})(jQuery, document);
﻿(function () {

    function show(options) {

        // items
        // positionTo
        // showCancel
        // title

        var innerOptions = {
            'title': options.title,
            'buttonLabels': options.items.map(function (i) {
                return i.name;
            })
        };

        // Show cancel unless the caller explicitly set it to false
        if (options.showCancel !== false) {
            innerOptions.addCancelButtonWithLabel = Globalize.translate('ButtonCancel');
        }

        // Depending on the buttonIndex, you can now call shareViaFacebook or shareViaTwitter
        // of the SocialSharing plugin (https://github.com/EddyVerbruggen/SocialSharing-PhoneGap-Plugin)
        window.plugins.actionsheet.show(innerOptions, function (index) {

            if (options.callback) {

                // Results are 1-based
                if (index >= 1 && options.items.length >= index) {
                    
                    options.callback(options.items[index - 1].id);
                }
            }
        });
    }

    window.ActionSheetElement = {
        show: show
    };
})();
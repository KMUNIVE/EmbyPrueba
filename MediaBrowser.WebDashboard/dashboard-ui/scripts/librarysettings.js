﻿(function ($, document, window) {

    function loadPage(page, config) {

        if (config.MergeMetadataAndImagesByName) {
            $('.fldImagesByName', page).hide();
        } else {
            $('.fldImagesByName', page).show();
        }

        $('#txtSeasonZeroName', page).val(config.SeasonZeroDisplayName);

        $('#selectEnableRealtimeMonitor', page).val(config.EnableLibraryMonitor).selectmenu("refresh");

        $('#txtItemsByNamePath', page).val(config.ItemsByNamePath || '');

        $('#chkEnableAudioArchiveFiles', page).checked(config.EnableAudioArchiveFiles).checkboxradio("refresh");
        $('#chkEnableVideoArchiveFiles', page).checked(config.EnableVideoArchiveFiles).checkboxradio("refresh");

        Dashboard.hideLoadingMsg();
    }

    function onSubmit() {
        Dashboard.showLoadingMsg();

        var form = this;

        ApiClient.getServerConfiguration().done(function (config) {

            config.ItemsByNamePath = $('#txtItemsByNamePath', form).val();

            config.SeasonZeroDisplayName = $('#txtSeasonZeroName', form).val();

            config.EnableLibraryMonitor = $('#selectEnableRealtimeMonitor', form).val();

            config.EnableAudioArchiveFiles = $('#chkEnableAudioArchiveFiles', form).checked();
            config.EnableVideoArchiveFiles = $('#chkEnableVideoArchiveFiles', form).checked();

            ApiClient.updateServerConfiguration(config).done(Dashboard.processServerConfigurationUpdateResult);
        });

        // Disable default form submission
        return false;
    }

    $(document).on('pageshowready', "#librarySettingsPage", function () {

        Dashboard.showLoadingMsg();

        var page = this;

        ApiClient.getServerConfiguration().done(function (config) {

            loadPage(page, config);

        });

    }).on('pageinitdepends', "#librarySettingsPage", function () {

        var page = this;

        $('#btnSelectIBNPath', page).on("click.selectDirectory", function () {

            var picker = new DirectoryBrowser(page);

            picker.show({

                callback: function (path) {

                    if (path) {
                        $('#txtItemsByNamePath', page).val(path);
                    }
                    picker.close();
                },

                header: Globalize.translate('HeaderSelectImagesByNamePath'),

                instruction: Globalize.translate('HeaderSelectImagesByNamePathHelp')
            });
        });

        $('.librarySettingsForm').off('submit', onSubmit).on('submit', onSubmit);
    });

})(jQuery, document, window);

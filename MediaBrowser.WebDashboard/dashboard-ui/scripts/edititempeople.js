﻿(function ($, document, window) {

    var currentItem;

    function reload(page) {

        var id = getParameterByName('id');

        Dashboard.showLoadingMsg();

        ApiClient.getItem(Dashboard.getCurrentUserId(), id).done(function (item) {

            if (item.IsFolder) {
                $('#fldRecursive', page).show();
            } else {
                $('#fldRecursive', page).hide();
            }

            $('#btnRefresh', page).button('enable');

            $('#refreshLoading', page).hide();

            currentItem = item;
            LibraryBrowser.renderName(item, $('.itemName', page), true);
            LibraryBrowser.renderParentName(item, $('.parentName', page));
            fillPeopleContainer(item.People,$('#peopleContainer',page));
            Dashboard.hideLoadingMsg();
        });
    }

    function fillPeopleContainer(people, container) {
        people = people || new Array();
        var html = '';
        for (var i = 0; i < people.length; i++) {
            html += constructPerson(people[i]);
        }

        container.html(html).trigger('create');
    }

    function constructPerson(person) {
        var html = '<div class="tileItem posterTileItem">';
        var imgUrl;
        var name = person.Name || "";
        var role = person.Role || "";
        var type = person.Type || "";
        
        if (person.PrimaryImageTag) {

            imgUrl = ApiClient.getPersonImageUrl(person.Name, {
                height: 280,
                tag: person.PrimaryImageTag,
                type: "primary"
            });

        } else {

            imgUrl = "css/images/items/list/person.png";
        }
        html += '<div class="tileImage" style="background-image: url(\''+imgUrl+'\');"></div>';
        html += '<div class="tileContent">';
        html += '<div data-role="fieldcontain">';
        html += '<input type="hidden" name="originalName" value="' + name + '">';
        html += '<input type="hidden" name="originalRole" value="' + role + '">';
        html += '<input type="hidden" name="originalType" value="' + type + '">';
        html += '<label for="txtName">Name:</label>';
        html += '<span class="read" style="font-size: 16px;line-height: 1.4;"> ' + (name) + '</span><span style="display:none;" class="edit">';
        html += '<input type="text" name="txtName" required="required" data-mini="true" value="' + (name) + '"/>';
        html += '</span>';
        html += '</div>';
        html += '<div data-role="fieldcontain">';
        html += '<label for="txtRole">Role:</label>';
        html += '<span class="read" style="font-size: 16px;line-height: 1.4;"> ' + (role) + '</span><span style="display:none;" class="edit">';
        html += '<input type="text" name="txtRole" required="required" data-mini="true" value="' + (role) + '"/>';
        html += '</span>';
        html += '</div>';
        html += '<div data-role="fieldcontain">';
        html += '<label for="selectType">Type:</label>';
        html += '<span class="read" style="font-size: 16px;line-height: 1.4;"> ' + (type) + '</span><span style="display:none;" class="edit">';
        html += '<select name="selectType">';
        html += generateTypes(type);
        html += '</select>';
        html += '</span>';
        html += '</div>';
        html += '<p>';
        html += '<span class="read">';
        html += '<button type="button" class="edit" data-mini="true" data-inline="true"  onclick="EditItemPeoplePage.displayEdit(this)">Edit</button>';
        html += '</span><span style="display:none;" class="edit">';
        html += '<button type="button" data-mini="true" data-inline="true" onclick="EditItemPeoplePage.hideEdit(this)">Cancel</button>';
        html += '<button type="button" data-icon="check" data-mini="true" data-inline="true" data-theme="b" onclick="EditItemPeoplePage.savePerson(this)">Save</button>';
        html += '<button type="button" data-icon="delete" data-mini="true" data-inline="true" data-iconpos="notext" onclick="EditItemPeoplePage.removePerson(this)">Delete</button>';
        html += '</span>';
        html += '</p>';
        html += '</div>';
        html += '</div>';
        return html;
    }

    function generateTypes(type) {
        var types = new Array("", "Actor", "Director", "Composer", "Writer", "GuestStar", "Producer");
        var html = "";

        for (var i = 0; i < types.length; i++) {
            html += '<option val="' + types[i] + '" ' + (types[i] == type ? 'selected="selected"' : '') + '>' + types[i] + '</option>';
        }

        return html;
    }
    function editItemPeoplePage() {

        var self = this;
        self.displayEdit = function (source) {
            $(source).parents('.tileItem').find('span.edit').show();
            $(source).parents('.tileItem').find('span.read').hide();
        };
        self.hideEdit = function (source) {
            var item = $(source).parents('.tileItem');
            item.find('span.edit').hide();
            item.find('span.read').show();
            item.find('input[name="txtName"]').val(item.find('input[name="originalName"]').val());
            item.find('input[name="txtRole"]').val(item.find('input[name="originalRole"]').val());
            item.find('select[name="selectType"]').val(item.find('input[name="originalType"]').val()).selectmenu('refresh');
        };

        self.removePerson = function (source) {

            var page = $.mobile.activePage;

            Dashboard.confirm("Are you sure you wish to delete this person?", "Delete Person", function (result) {
                if (result) {
                    var item = $(source).parents('.tileItem');
                    var originalName = item.find('input[name="originalName"]').val();
                    var originalRole = item.find('input[name="originalRole"]').val();
                    var originalType = item.find('input[name="originalType"]').val();
                    for (var i = 0; i < currentItem.People.length; i++) {
                        var name = currentItem.People[i].Name || "";
                        var role = currentItem.People[i].Role || "";
                        var type = currentItem.People[i].Type || "";
                        if ((name + role + type) == (originalName + originalRole + originalType)) {
                            currentItem.People.splice(i, 1);
                            ApiClient.updateItem(currentItem).done(function () {
                                reload(page);
                            });
                        }
                    }

                }
            });
        };
        self.savePerson = function(source) {

            var page = $.mobile.activePage;

            var item = $(source).parents('.tileItem');
            var originalName = item.find('input[name="originalName"]').val();
            var originalRole = item.find('input[name="originalRole"]').val();
            var originalType = item.find('input[name="originalType"]').val();
            for (var i = 0; i < currentItem.People.length; i++) {
                var name = currentItem.People[i].Name || "";
                var role = currentItem.People[i].Role || "";
                var type = currentItem.People[i].Type || "";
                if ((name + role + type) == (originalName + originalRole + originalType)) {
                    currentItem.People[i].Name = item.find('input[name="txtName"]').val();
                    currentItem.People[i].Role = item.find('input[name="txtRole"]').val();
                    currentItem.People[i].Type = item.find('select[name="selectType"]').val();
                    ApiClient.updateItem(currentItem).done(function() {
                        reload(page);
                    });
                    break;
                }
            }
        };

        self.addPerson = function() {
            var page = $.mobile.activePage;

            var html = '<div data-role="popup" id="popupCreatePerson" class="ui-corner-all popup" style=" width: 270px;" >';

            html += '<div class="ui-corner-top ui-bar-a" style="text-align: center; padding: 0 20px;">';
            html += '<h3>Add Person</h3>';
            html += '</div>';

            html += '<div data-role="content" class="ui-corner-bottom ui-content" style="padding:10px;">';
            html += '<form>';
            html += '<label for="txtPersonName">Name:</label>';
            html += '<input type="text" id="txtPersonName" name="txtPersonName" required="required"/>';
            
            html += '<label for="txtPersonRole">Role:</label>';
            html += '<input type="text" id="txtPersonRole" name="txtPersonRole" style="font-weight:bold;" />';
            
            html += '<label for="selectPersonType">Type:</label>';
            html += '<select id="selectPersonType" name="selectPersonType">';
            html += generateTypes('');
            html += '</select>';

            html += '<p>';
            html += '<button type="submit" data-theme="b" data-icon="ok">OK</button>';
            html += '<button type="button" data-icon="delete" onclick="$(this).parents(\'.popup\').popup(\'close\');">Cancel</button>';
            html += '</p>';
            html += '</form>';
            html += '</div>';
            html += '</div>';

            $(page).append(html);

            var popup = $('#popupCreatePerson').popup().trigger('create').on("popupafteropen", function() {
                $('#popupCreatePerson input:first', this).focus();
            }).popup("open").on("popupafterclose", function() {

                $('form', this).off("submit");
                $(this).off("click").off("popupafterclose").remove();

            });
            
            $('form', popup).on('submit', function () {

                var form = $(this);

                var name = $('#txtPersonName',form).val();
                if (name != '') {
                    var role = $('#txtPersonRole', form).val();
                    var type = $('#selectPersonType', form).val();
                    currentItem.People.push({ Name: name, Role: role, Type: type });
                    ApiClient.updateItem(currentItem).done(function () {
                        reload(page);
                    });
                    popup.popup("close");
                }
                return false;
            });
        };
    }

    window.EditItemPeoplePage = new editItemPeoplePage();

    $(document).on('pageinit', "#editItemPeoplePage", function () {

        var page = this;

    }).on('pageshow', "#editItemPeoplePage", function () {

        var page = this;

        reload(page);

    }).on('pagehide', "#editItemPeoplePage", function () {

        var page = this;

        currentItem = null;
    });

})(jQuery, document, window);
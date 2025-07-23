/*
* jQuery QueryBuilder Elasticsearch 'bool' query support
* https://github.com/mistic100/jQuery-QueryBuilder
* https://www.elastic.co/
* https://www.elastic.co/guide/en/elasticsearch/reference/current/query-dsl-bool-query.html
*/

// Register plugin
(function (root, factory) {
    if (typeof define === 'function' && define.amd) {
        define(['jquery', 'query-builder'], factory);
    }
    else {
        factory(root.jQuery);
    }
}(this, function ($) {
    "use strict";

    var QueryBuilder = $.fn.queryBuilder;

    // DEFAULT CONFIG
    // ===============================
    QueryBuilder.defaults({
        ESBoolOperators: {
            match: function (v) { return v; }, 
            do_not_match: function (v) { return v; },
            less: function (v) { return { 'lt': v }; },
            less_or_equal: function (v) { return { 'lte': v }; },
            greater: function (v) { return { 'gt': v }; },
            greater_or_equal: function (v) { return { 'gte': v }; },
            contains: function (v) { return `*${v}*`; },
            not_contains: function (v) { return `*${v}*`; },
            begins_with: function (v) { return `${v}*`; },
            not_begins_with: function (v) { return `${v}*`; },
            ends_with: function (v) { return `*${v}`; },
            not_ends_with: function (v) { return `*${v}`; },
            between: function (v) { return { 'gte': v[0], 'lte': v[1] }; },
            // in: function (v) { return v.split(',').map(function (e) { return e.trim(); }); },
            // not_in: function (v) { return v.split(',').map(function (e) { return e.trim(); }); },
            is_in: function (v) {
                const allTypes = '.pdf, .docx, .doc, .xlsx, .xls, .txt, .pptx, .ppt, .rtf';
                if( v === 'Other') {
                    return allTypes;
                }
                return v;
            },
            is_not_in: function (v) {
                const allTypes = '.pdf, .docx, .doc, .xlsx, .xls, .txt, .pptx, .ppt, .rtf';
                if( v === 'Other') {
                    return allTypes;
                }
                return v;
            },
            in: function (v) {
                const allTypes = ['.pdf', '.docx', '.doc', '.xlsx', '.xls', '.txt', '.pptx', '.ppt', '.rtf'];

                if (v === 'Other') {
                    return allTypes;
                }

                return Array.isArray(v) ? v : String(v).split(',').map(e => e.trim());
            },
            not_in: function (v) {
                const allTypes = ['.pdf', '.docx', '.doc', '.xlsx', '.xls', '.txt', '.pptx', '.ppt', '.rtf'];

                if (v === 'Other') {
                    return allTypes;
                }

                return Array.isArray(v) ? v : String(v).split(',').map(e => e.trim());
            },
            is_null: function (v) { return v; },
            is_not_null: function (v) { return v; },
            fuzzy: function (v) {
                console.log("Fuzzy search ES query: ", v);
                return { query: v, fuzziness: 'AUTO' };
            },
            equal: function (v) {
                return v;
            },
            not_equal: function (v) {
                return v;
            }
            // equal: function (v) {
            //     const allTypes = ['.pdf', '.docx', '.doc', '.xlsx', '.xls', '.txt', '.pptx', '.ppt', '.rtf'];

            //     if (v === 'Other') {
            //         return allTypes;
            //     }

            //     // return v;
            //     const values = v.split(/[\/|]/).map(x => x.trim());
            //     console.log("Values in equal operator: ", values);
            //     return values.length > 1 ? values : values[0];
            //     // return values;
            // }
        },
        ESQueryStringQueryOperators: {
            is_not_null: function () { return "_exists_:"; },
            is_null: function () { return "_missing_:"; },
            contains: function (v) { return `*${v}*`; },
            between: function (v) { return '[' + v[0] + ' TO ' + v[1] + "]"; },
        }
    });


    // PUBLIC METHODS
    // ===============================
    QueryBuilder.extend({
        /**
        * Get rules as an elasticsearch bool query
        * @param data {object} (optional) rules
        * @return {object}
        */
        getESBool: function (data) {
            data = (data === undefined) ? this.getRules() : data;

            var that = this;

            return (function parse(data) {
                if (!data.condition) {
                    data.condition = that.settings.default_condition;
                }

                if (['AND', 'OR'].indexOf(data.condition.toUpperCase()) === -1) {
                    throw new Error(
                        'Unable to build Elasticsearch bool query with condition "{0}"'
                            .replace('{0}', data.condition)
                    );
                }

                if (!data.rules) {
                    return {};
                }

                var parts = {};
                parts.add = function (k, v) {
                    if (this.hasOwnProperty(k)) { this[k].push(v) }
                    else { this[k] = [v] }
                };

                data.rules.forEach(function (rule) {

                    function get_value(rule) {
                        if (rule.data && rule.data.hasOwnProperty('transform')) {
                            return window[rule.data.transform].call(this, rule.value);
                        } else {
                            return rule.value;
                        }
                    }

                    function make_query(rule) {
                        var mdb = that.settings.ESBoolOperators[rule.operator],
                            ope = that.getOperatorByType(rule.operator),
                            part = {};

                        if (mdb === undefined) {
                            throw new Error(
                                'Unknown elasticsearch operation for operator "{0}"'
                                    .replace('{0}', rule.operator)
                            );
                        }

                        if (ope.nb_inputs !== 0) {
                            var es_key_val = {};
                            es_key_val[rule.field] = mdb.call(that, get_value(rule));
                            part[getQueryDSLWord(rule)] = es_key_val;
                        }

                        if (rule.operator === 'is_null' || rule.operator === 'is_not_null') {
                            part = { exists: { field: rule.field } };
                        }

                        // this is a corner case, when we have an "or" group and a negative operator,
                        // we express this with a sub boolean query and must_not.
                        if (data.condition === 'OR' && (rule.operator === 'not_equal' || rule.operator === 'not_in' || rule.operator === 'is_null' || rule.operator === 'not_contains' || rule.operator === 'not_begins_with' || rule.operator === 'not_ends_with' || rule.operator === 'do_not_match' || (rule.operator === 'is_not_in' && rule.value !== 'Other') || (rule.operator === 'is_in' && rule.value === 'Other'))) {
                            return { 'bool': { 'must_not': [part] } }
                        } else {
                            return part
                        }
                    }

                    var clause = getClauseWord(data.condition, rule.operator, rule.value);

                    if (rule.rules && rule.rules.length > 0) {
                        parts.add(clause, parse(rule));
                    } else {
                        parts.add(clause, make_query(rule));
                    }

                });

                delete parts.add;
                return { 'bool': parts }
            }(data));
        },

        /**
        * Get rules as an elasticsearch query string query
        * @param data {object} (optional) rules
        * @return {object}
        */
        getESQueryStringQuery: function (data) {
            data = (data === undefined) ? this.getRules() : data;

            var that = this;

            return (function parse(data) {
                if (!data.condition) {
                    data.condition = that.settings.default_condition;
                }

                if (['AND', 'OR'].indexOf(data.condition.toUpperCase()) === -1) {
                    throw new Error(
                        'Unable to build Elasticsearch query String query with condition "{0}"'
                            .replace('{0}', data.condition)
                    );
                }

                if (!data.rules) {
                    return "";
                }

                // generate query string
                var parts = "";

                data.rules.forEach(function (rule, index) {
                    function get_value(rule) {
                        return rule.value;
                    }

                    function make_query(rule) {
                        var mdb = that.settings.ESQueryStringQueryOperators[rule.operator],
                            ope = that.getOperatorByType(rule.operator),
                            part = "";

                        if (mdb === undefined) {
                            throw new Error(
                                'Unknown elasticsearch operation for operator "{0}"'
                                    .replace('{0}', rule.operator)
                            );
                        }

                        var es_key_val = "";
                        if (ope.nb_inputs !== 0) {
                            es_key_val += rule.field + ":" + mdb.call(that, rule.value);
                            part += es_key_val;
                        }
                        else if (ope.nb_inputs === 0) {
                            es_key_val += mdb.call(that, rule.value) + rule.field;
                            part += es_key_val;
                        }

                        if (data.rules[index + 1]) {
                            return part + " " + data.condition + " ";
                        }
                        else {
                            return part;
                        }

                    }
                    if (rule.rules && rule.rules.length > 0) {
                        parts += "(" + parse(rule) + ")";
                    } else {
                        parts += make_query(rule);
                    }

                });
                return parts;
            }(data));
        }
    });

    function getQueryDSLWord(rule) {
        const wildcard = /^(contains|not_contains|begins_with|not_begins_with|ends_with|not_ends_with)$/.exec(rule.operator);
        const terms = /^(in|not_in)$/.exec(rule.operator);
        const range = /^(less|less_or_equal|greater|greater_or_equal|between)$/.exec(rule.operator);
        // const is_in = /^(is_in|is_not_in)$/.exec(rule.operator);
        // const fuzzy = /^(fuzzy)$/.exec(rule.operator);
        const match_phrase = /^(equal|not_equal)$/.exec(rule.operator);

        if (wildcard !== null) {
            return 'wildcard';
        }
        if (terms !== null) {
            return 'terms';
        }
        if (range !== null) {
            return 'range';
        }
        if (match_phrase !== null) {
            return 'match_phrase';
        }

        // Default all "match" or other operators to match
        return 'match';
    }

    /**
    * Get the right type of clause in the bool query
    */
    function getClauseWord(condition, operator, value) {
        if (condition === 'AND' && (operator !== 'not_equal' && operator !== 'not_in' && operator !== 'not_contains' && operator !== 'not_begins_with' && operator !== 'not_ends_with' && operator !== 'do_not_match' && (operator !== 'is_not_in' && value !== 'Other'))) { return 'must' }
        if (condition === 'AND' && (operator === 'not_equal' || operator == 'not_in' || operator === 'not_contains' || operator === 'not_begins_with' || operator === 'not_ends_with' || operator === 'do_not_match' || ((operator === 'is_not_in' && value !== 'Other')))) { return 'must_not' }
        if(condition === 'AND' && operator === 'is_in' && value === 'Other') { return 'must_not' }
        if(condition === 'AND' && operator === 'is_not_in' && value === 'Other') { return 'must' }
        if (condition === 'OR') { return 'should' }    
    }
}));

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using AutoRest.Core;
using AutoRest.Core.Model;
using AutoRest.Core.Utilities;
using AutoRest.CSharp.Azure.Fluent.Model;
using AutoRest.CSharp.Azure.Model;
using AutoRest.CSharp.Model;
using AutoRest.Extensions.Azure;
using Pluralize.NET;

namespace AutoRest.CSharp.Azure.Fluent
{
    public class TransformerCsaf : TransformerCsa, ITransformer<CodeModelCsaf>
    {
        private Pluralizer pluralizer = new Pluralizer();
        private List<string> addInner = new List<string>();
        private List<string> removeInner = new List<string>();

        CodeModelCsaf ITransformer<CodeModelCsaf>.TransformCodeModel(CodeModel cs)
        {
            var codeModel = cs as CodeModelCsaf;
            Settings.Instance.AddCredentials = true;

            // todo: these should be turned into individual transformers
            AzureExtensions.NormalizeAzureClientModel(codeModel);

            // Do parameter transformations
            TransformParameters(codeModel);

            // Fluent Specific stuff.
            MoveResourceTypeProperties(codeModel); // call this before normalizing the resource types
            NormalizeResourceTypes(codeModel);
            NormalizeTopLevelTypes(codeModel);
            NormalizeModelProperties(codeModel);


            NormalizePaginatedMethods(codeModel);
            NormalizeODataMethods(codeModel);

            return codeModel;
        }

        /// <summary>
        ///     A type-specific method for code model tranformation.
        ///     Note: This is the method you want to override.
        /// </summary>
        /// <param name="codeModel"></param>
        /// <returns></returns>
        public override CodeModelCs TransformCodeModel(CodeModel codeModel)
        {
            return ((ITransformer<CodeModelCsaf>) this).TransformCodeModel(codeModel);
        }

        public void NormalizeResourceTypes(CodeModelCsaf codeModel)
        {
            if (codeModel != null)
            {
                foreach (var model in codeModel.ModelTypes)
                {
                    if (model.BaseModelType is CompositeTypeCsaf type && type.IsResource)
                    {
                        model.BaseModelType = RealResourceType(type, codeModel);
                    }
                    model.Properties.ForEach(p =>
                    {
                        if (p.ModelType.IsResource())
                        {
                            p.ModelType = RealResourceType((CompositeTypeCsaf) p.ModelType, codeModel);
                        }
                        else if (p.ModelType.IsResourceArray())
                        {
                            ((SequenceType)p.ModelType).ElementType = RealResourceType((CompositeTypeCsaf)((SequenceType)p.ModelType).ElementType, codeModel);
                        }
                        else if (p.ModelType.IsResourceMap())
                        {
                            ((DictionaryType)p.ModelType).ValueType = RealResourceType((CompositeTypeCsaf)((DictionaryType)p.ModelType).ValueType, codeModel);
                        }
                    });
                }
                foreach(var method in codeModel.Methods)
                {
                    method.Responses.ForEach(r =>
                    {
                        if (r.Value.Body.IsResource())
                        {
                            r.Value.Body = RealResourceType((CompositeTypeCsaf)r.Value.Body, codeModel);
                        }
                        else if (r.Value.Body.IsResourceArray())
                        {
                            ((SequenceType)r.Value.Body).ElementType = RealResourceType((CompositeTypeCsaf)((SequenceType)r.Value.Body).ElementType, codeModel);
                        }
                        else if (r.Value.Body.IsResourceMap())
                        {
                            ((DictionaryType)r.Value.Body).ValueType = RealResourceType((CompositeTypeCsaf)((DictionaryType)r.Value.Body).ValueType, codeModel);
                        }
                    });
                    if (method.ReturnType.Body.IsResource())
                    {
                        method.ReturnType.Body = RealResourceType((CompositeTypeCsaf)method.ReturnType.Body, codeModel);
                    }
                    else if (method.ReturnType.Body.IsResourceArray())
                    {
                        ((SequenceType)method.ReturnType.Body).ElementType = RealResourceType((CompositeTypeCsaf)((SequenceType)method.ReturnType.Body).ElementType, codeModel);
                    }
                    else if (method.ReturnType.Body.IsResourceMap())
                    {
                        ((DictionaryType)method.ReturnType.Body).ValueType = RealResourceType((CompositeTypeCsaf)((DictionaryType)method.ReturnType.Body).ValueType, codeModel);
                    }
                    method.Parameters.ForEach(p =>
                    {
                        if (p.ModelType.IsResource())
                        {
                            p.ModelType = RealResourceType((CompositeTypeCsaf)p.ModelType, codeModel);
                        }
                        else if (p.ModelType.IsResourceArray())
                        {
                            ((SequenceType)p.ModelType).ElementType = RealResourceType((CompositeTypeCsaf)((SequenceType)p.ModelType).ElementType, codeModel);
                        }
                        else if (p.ModelType.IsResourceMap())
                        {
                            ((DictionaryType)p.ModelType).ValueType = RealResourceType((CompositeTypeCsaf)((DictionaryType)p.ModelType).ValueType, codeModel);
                        }
                    });
                }
            }
        }

        private CompositeTypeCsaf RealResourceType(CompositeTypeCsaf type, CodeModelCsaf codeModel)
        {
            switch (type.ModelResourceType)
            {
                case ResourceType.ProxyResource:
                    return codeModel._proxyResourceType;
                case ResourceType.Resource:
                    if (type.Properties.First(p => p.SerializedName == "location").IsRequired)
                    {
                        return codeModel._resourceType;
                    }
                    else
                    {
                        return codeModel._resourceTypeNoValidate;
                    }
                case ResourceType.SubResource:
                    return codeModel._subResourceType;
                default:
                    return type;
            }
        }

        public virtual void MoveResourceTypeProperties(CodeModel client)
        {
            if (client == null)
            {
                throw new ArgumentNullException("client");
            }

            foreach (CompositeTypeCsaf subtype in client.ModelTypes.Where(t => t.BaseModelType.IsResource()))
            {
                var baseType = subtype.BaseModelType as CompositeTypeCsaf;
                if (baseType.ModelResourceType == ResourceType.SubResource)
                {
                    foreach (var prop in baseType.Properties.Where(p => p.SerializedName != "id"))
                    {
                        subtype.Add(prop);
                    }
                }
                else if (baseType.ModelResourceType == ResourceType.ProxyResource)
                {
                    foreach (var prop in baseType.Properties.Where(p => p.SerializedName != "id" && p.SerializedName != "name" && p.SerializedName != "type"))
                    {
                        subtype.Add(prop);
                    }
                    foreach (var prop in baseType.Properties.Where(p => p.SerializedName == "id" || p.SerializedName == "name" || p.SerializedName == "type"))
                    {
                        if (!prop.IsReadOnly)
                        {
                            subtype.Add(prop);
                        }
                    }
                }
                else if (baseType.ModelResourceType == ResourceType.Resource)
                {
                    foreach (var prop in baseType.Properties.Where(p => p.SerializedName != "id" && p.SerializedName != "name" && p.SerializedName != "type" && p.SerializedName != "location" && p.SerializedName != "tags"))
                    {
                        subtype.Add(prop);
                    }
                    foreach (var prop in baseType.Properties.Where(p => p.SerializedName == "id" || p.SerializedName == "name" || p.SerializedName == "type"))
                    {
                        if (!prop.IsReadOnly)
                        {
                            var newProp = new PropertyCsaf
                            {
                                SerializedName = null, // Indicates PropertyFlavor.Implementation
                                Name = prop.Name,
                                Documentation = prop.Documentation,
                                Summary = prop.Summary,
                                IsReadOnly = prop.IsReadOnly,
                                ModelType = prop.ModelType,
                                ResourcePropertyOverride = true,
                                Implementation = new Dictionary<string, string>()
                                {
                                    {"csharp", new IndentedStringBuilder().Indent().AppendLine("get { return base." + prop.Name + "; }").AppendLine("set { base." + prop.Name + " = value; }").Outdent().ToString() }
                                }
                            };
                            subtype.ExtraProperties.Add(newProp);
                        }
                    }
                    var locationProp = baseType.Properties.First(p => p.SerializedName == "location");
                    if (!locationProp.IsRequired)
                    {
                        subtype.Add(new PropertyCsaf { Name = "fluentdummy", ModelType = new PrimaryTypeCs(KnownPrimaryType.String), RequiredPropertyOverride = true });
                    }
                }
            }
        }

        public void NormalizeTopLevelTypes(CodeModelCsaf codeModel)
        {
            var included = AutoRest.Core.Settings.Instance.Host?.GetValue<string>("add-inner").Result;
            if (included != null)
            {
                included.Split(',', StringSplitOptions.RemoveEmptyEntries).ForEach(addInner.Add);
            }
            var excluded = AutoRest.Core.Settings.Instance.Host?.GetValue<string>("remove-inner").Result;
            if (excluded != null)
            {
                excluded.Split(',', StringSplitOptions.RemoveEmptyEntries).ForEach(removeInner.Add);
            }

            foreach (var response in codeModel.Methods
                .SelectMany(m => m.Responses)
                .Select(r => r.Value))
            {
                AppendInnerToTopLevelType(response.Body, codeModel);
            }
            foreach (var model in codeModel.ModelTypes)
            {
                if (addInner.Contains(model.Name))
                {
                    AppendInnerToTopLevelType(model, codeModel);
                }
                if (model.BaseModelType != null && model.BaseModelType.IsResource())
                {
                    AppendInnerToTopLevelType(model, codeModel);
                }
                else if (codeModel.Operations.Any(o => o.Name.EqualsIgnoreCase(model.Name) || o.Name.EqualsIgnoreCase(pluralizer.Pluralize(model.Name)))) // Naive plural check
                {
                    AppendInnerToTopLevelType(model, codeModel);
                }
            }
        }

        private void AppendInnerToTopLevelType(IModelType type, CodeModelCsaf codeModel)
        {
            if (type == null || removeInner.Contains(type.Name))
            {
                return;
            }
            CompositeTypeCsaf compositeType = type as CompositeTypeCsaf;
            SequenceType sequenceType = type as SequenceType;
            DictionaryType dictionaryType = type as DictionaryType;
            if (compositeType != null && !compositeType.IsResource)
            {
                compositeType.IsInnerModel = true;
                foreach (var t in codeModel.ModelTypes)
                {
                    foreach (var p in t.Properties.Where(p => p.ModelType is CompositeTypeCsaf && !((CompositeTypeCsaf)p.ModelType).IsInnerModel))
                    {
                        if (p.ModelTypeName.EqualsIgnoreCase(compositeType.Name)
                            || (p.ModelType is SequenceType && ((SequenceType)p.ModelType).ElementType.Name.EqualsIgnoreCase(compositeType.Name))
                            || (p.ModelType is DictionaryType && ((DictionaryType)p.ModelType).ValueType.Name.EqualsIgnoreCase(compositeType.Name)))
                        {
                            AppendInnerToTopLevelType(t, codeModel);
                            break;
                        }
                    }
                }
            }
            else if (sequenceType != null)
            {
                AppendInnerToTopLevelType(sequenceType.ElementType, codeModel);
            }
            else if (dictionaryType != null)
            {
                AppendInnerToTopLevelType(dictionaryType.ValueType, codeModel);
            }
        }

        public void NormalizeModelProperties(CodeModelCsa serviceClient)
        {
            foreach (var modelType in serviceClient.ModelTypes)
            {
                foreach (var property in modelType.Properties)
                {
                    AddNamespaceToResourceType(property.ModelType, serviceClient);
                }
            }
        }

        private void AddNamespaceToResourceType(IModelType type, CodeModelCsa serviceClient)
        {
            // iList<SubResource> property { get; set; } => iList<Microsoft.Rest.Azure.SubResource> property { get; set; }
            if (type is SequenceType sequenceType)
            {
                AddNamespaceToResourceType(sequenceType.ElementType, serviceClient);
            }
            // IDictionary<string, SubResource> property { get; set; } => IDictionary<string, Microsoft.Rest.Azure.SubResource> property { get; set; }
            else if (type is DictionaryType dictionaryType)
            {
                AddNamespaceToResourceType(dictionaryType.ValueType, serviceClient);
            }
        }
    }
}
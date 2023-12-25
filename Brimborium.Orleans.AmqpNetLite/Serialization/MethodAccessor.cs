//  ------------------------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation
//  All rights reserved. 
//  
//  Licensed under the Apache License, Version 2.0 (the ""License""); you may not use this 
//  file except in compliance with the License. You may obtain a copy of the License at 
//  http://www.apache.org/licenses/LICENSE-2.0  
//  
//  THIS CODE IS PROVIDED *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, 
//  EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY IMPLIED WARRANTIES OR 
//  CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE, MERCHANTABLITY OR 
//  NON-INFRINGEMENT. 
// 
//  See the Apache Version 2.0 License for specific language governing permissions and 
//  limitations under the License.
//  ------------------------------------------------------------------------------------

namespace Brimborium.OrleansAmqp.Serialization;

internal abstract class MethodAccessor {
    private delegate object MethodDelegate(object container, object[] parameters);

    private static readonly Type[] _DelegateParamsType = { typeof(object), typeof(object[]) };
    private bool _IsStatic;
    private MethodDelegate _MethodDelegate;

    public static MethodAccessor Create(MethodInfo methodInfo) {
        return new TypeMethodAccessor(methodInfo);
    }

    public static MethodAccessor Create(ConstructorInfo constructorInfo) {
        return new ConstructorAccessor(constructorInfo);
    }

    public object Invoke(object[] parameters) {
        if (!this._IsStatic) {
            throw new InvalidOperationException("Instance required to call an instance method.");
        }

        return this.Invoke(null, parameters);
    }

    public object Invoke(object container, object[] parameters) {
        if (this._IsStatic && container != null) {
            throw new InvalidOperationException("Static method must be called with null instance.");
        }

        return this._MethodDelegate(container, parameters);
    }

    private static Type[] GetParametersType(ParameterInfo[] paramsInfo) {
        Type[] paramsType = new Type[paramsInfo.Length];
        for (int i = 0; i < paramsInfo.Length; i++) {
            paramsType[i] = paramsInfo[i].ParameterType.IsByRef ? paramsInfo[i].ParameterType.GetElementType() : paramsInfo[i].ParameterType;
        }

        return paramsType;
    }

    private static void LoadArguments(ILGenerator generator, Type[] paramsType) {
        for (int i = 0; i < paramsType.Length; i++) {
            generator.Emit(OpCodes.Ldarg_1);
            switch (i) {
                case 0: generator.Emit(OpCodes.Ldc_I4_0); break;
                case 1: generator.Emit(OpCodes.Ldc_I4_1); break;
                case 2: generator.Emit(OpCodes.Ldc_I4_2); break;
                case 3: generator.Emit(OpCodes.Ldc_I4_3); break;
                case 4: generator.Emit(OpCodes.Ldc_I4_4); break;
                case 5: generator.Emit(OpCodes.Ldc_I4_5); break;
                case 6: generator.Emit(OpCodes.Ldc_I4_6); break;
                case 7: generator.Emit(OpCodes.Ldc_I4_7); break;
                case 8: generator.Emit(OpCodes.Ldc_I4_8); break;
                default: generator.Emit(OpCodes.Ldc_I4, i); break;
            }
            generator.Emit(OpCodes.Ldelem_Ref);
            if (paramsType[i].IsValueType()) {
                generator.Emit(OpCodes.Unbox_Any, paramsType[i]);
            } else if (paramsType[i] != typeof(object)) {
                generator.Emit(OpCodes.Castclass, paramsType[i]);
            }
        }
    }

    private sealed class ConstructorAccessor : MethodAccessor {
        public ConstructorAccessor(ConstructorInfo constructorInfo) {
            this._IsStatic = true;
            DynamicMethod method = new DynamicMethod("ctor_" + constructorInfo.DeclaringType!.Name, typeof(object), _DelegateParamsType, true);
            Type[] paramsType = GetParametersType(constructorInfo.GetParameters());
            ILGenerator generator = method.GetILGenerator();
            LoadArguments(generator, paramsType);
            generator.Emit(OpCodes.Newobj, constructorInfo);
            if (constructorInfo.DeclaringType.IsValueType()) {
                generator.Emit(OpCodes.Box, constructorInfo.DeclaringType);
            }
            generator.Emit(OpCodes.Ret);

            this._MethodDelegate = (MethodDelegate)method.CreateDelegate(typeof(MethodDelegate));
        }
    }

    private sealed class TypeMethodAccessor : MethodAccessor {
        public TypeMethodAccessor(MethodInfo methodInfo) {
            Type[] paramsType = GetParametersType(methodInfo.GetParameters());
            DynamicMethod method = new DynamicMethod("method_" + methodInfo.Name, typeof(object), _DelegateParamsType, true);
            ILGenerator generator = method.GetILGenerator();
            if (!this._IsStatic) {
                generator.Emit(OpCodes.Ldarg_0);
                if (methodInfo.DeclaringType.IsValueType()) {
                    generator.Emit(OpCodes.Unbox_Any, methodInfo.DeclaringType!);
                } else {
                    generator.Emit(OpCodes.Castclass, methodInfo.DeclaringType!);
                }
            }
            LoadArguments(generator, paramsType);
            if (methodInfo.IsFinal) {
                generator.Emit(OpCodes.Call, methodInfo);
            } else {
                generator.Emit(OpCodes.Callvirt, methodInfo);
            }

            if (methodInfo.ReturnType == typeof(void)) {
                generator.Emit(OpCodes.Ldnull);
            } else if (methodInfo.ReturnType.IsValueType()) {
                generator.Emit(OpCodes.Box, methodInfo.ReturnType);
            }

            generator.Emit(OpCodes.Ret);

            this._MethodDelegate = (MethodDelegate)method.CreateDelegate(typeof(MethodDelegate));
        }
    }
}

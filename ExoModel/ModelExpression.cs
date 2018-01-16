using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Collections.ObjectModel;
using Expr = System.Linq.Expressions.Expression;
using System.Globalization;
using System.Text.RegularExpressions;
using System.ComponentModel;

#pragma warning disable 618

namespace ExoModel
{
	/// <summary>
	/// Represents an expression that can be invoked against the model to perform an
	/// action or return a value.  The specified expression will be automatically analysed to
	/// expose a <see cref="ModelPath"/> representing the dependencies of the expression.
	/// </summary>
	/// <remarks>
	/// Call <see cref="ModelType.GetExpression(string)"/> to create a <see cref="ModelExpression"/> based on a
	/// <see cref="String"/> representation.
	/// </remarks>
	public class ModelExpression
	{
		#region Fields

		private Delegate compiledExp = null;

		private ExpressionFormatter formatter;

		#endregion

		#region Constructors

		public ModelExpression(ModelType rootType, LambdaExpression expression)
		{
			this.RootType = rootType;
			this.Expression = expression;
			this.Path = rootType.GetPath(expression);
		}

		internal ModelExpression(ModelType rootType, string expression, Type resultType, QuerySyntax querySyntax = QuerySyntax.DotNet)
		{
			// Save the root type of the expression
			this.RootType = rootType;

			// Parse the expression
			var parameters = new ModelParameterExpression[] { new ModelParameterExpression(new ModelExpressionType(rootType, false), "") };
			var resultExpression = new ExpressionParser(parameters, expression, querySyntax, false).Parse(resultType == null ? null : new ModelExpressionType(resultType));

			// Determine the model property paths referenced by the expression
			this.Path = rootType.GetPath(resultExpression);

			// Compile the expression to make it executable
			if (rootType is IExpressionCompiler)
				this.Expression = ((IExpressionCompiler)rootType).Compile(resultExpression, parameters[0]);
			else
				this.Expression = ModelExpression.ExpressionCompiler.Compile(resultExpression, parameters[0]);
		}

		#endregion

		#region Properties

		public LambdaExpression Expression { get; private set; }

		private ExpressionFormatter Formatter
		{
			get
			{
				if (formatter == null)
					formatter = new ExpressionFormatter(this, CultureInfo.CurrentCulture);
				return formatter;
			}
		}

		public Delegate CompiledExpression
		{
			get
			{
				if (compiledExp == null)
					compiledExp = Expression.Compile();

				return compiledExp;
			}
		}

		public ModelType RootType { get; private set; }

		public ModelPath Path { get; private set; }

		#endregion

		#region Methods

		public static Expression Parse(ModelExpressionType resultType, string expression, params object[] values)
		{
			return Parse(resultType, null, expression, values);
		}

		public static Expression Parse(ModelExpressionType resultType, ModelType rootType, string expression, params object[] values)
		{
			var parameters = rootType == null ? null : new ModelParameterExpression[] { new ModelParameterExpression(new ModelExpressionType(rootType, false), "") };
			ExpressionParser parser = new ExpressionParser(parameters, expression, values);

			return parser.Parse(resultType);
		}

		public static IntelliSense ParseIntelliSense(ModelType rootType, string expression, params object[] values)
		{
			ModelParameterExpression[] parameters = (rootType == null ? null : new ModelParameterExpression[] { new ModelParameterExpression(new ModelExpressionType(rootType, false), "") });
			ExpressionParser parser = null;

			// If parser initialization fails (expressions starts with ", /, etc.) return IntelliSense that would be initialized in ExpressionParser constructor
			try
			{
				parser = new ExpressionParser(parameters, expression, values);
			}
			catch
			{
				return new IntelliSense() { Position = 0, Type = parameters == null ? null : parameters[0], Scope = parameters == null || parameters[0] == null ? IntelliSenseScope.Globals : IntelliSenseScope.Globals | IntelliSenseScope.InstanceMembers };
			}

			// Parse the expression, ignoring parse errors
			try
			{
				parser.Parse(null);
			}
			catch
			{ }

			// Return the IntelliSense data
			return parser.IntelliSense;
		}

		internal static Type CreateClass(params DynamicProperty[] properties)
		{
			return ClassFactory.Instance.GetDynamicClass(properties);
		}

		internal static Type CreateClass(IEnumerable<DynamicProperty> properties)
		{
			return ClassFactory.Instance.GetDynamicClass(properties);
		}

		public ModelType GetResultModelType(Func<UnaryExpression, ModelProperty> getDynamicMemberAccess = null)
		{
			ModelType resultType;
			bool resultIsList;

			if (!ExpressionHelper.TryGetResultModelType(Expression, RootType, getDynamicMemberAccess, out resultType, out resultIsList))
				return null;

			return resultType;
		}

		/// <summary>
		/// Invokes the expression for the specified root instance.
		/// </summary>
		/// <param name="root"></param>
		/// <returns></returns>
		public object Invoke(ModelInstance root)
		{
			// Expressions that do not require root instances (like static properties or constant expressions)
			if (Expression.Parameters.Count == 0)
				return CompiledExpression.DynamicInvoke();

			// Expressions requiring a root instance
			else
			{
				// Make sure a root instance was specified for expressions requiring a root instance
				if (root == null)
					throw new ArgumentNullException("root", "A model instance must be specified to invoke this model expression.");

				// Make sure the root instance is of the correct model type
				if (!RootType.IsInstanceOfType(root))
					throw new ArgumentException("The specified model instance of type " + root.Type + " is not valid for this expression.  Expected instance of type " + RootType);

				return CompiledExpression.DynamicInvoke(root.Instance);
			}
		}

		/// <summary>
		/// Gets the value of the expression.
		/// </summary>
		/// <param name="instance">The root model instance.</param>
		/// <returns>The value of the expression.</returns>
		public object GetValue(ModelInstance instance)
		{
			return Invoke(instance);
		}

		/// <summary>
		/// Gets the value of the expression formatted as a string.
		/// </summary>
		/// <param name="instance">The root model instance.</param>
		/// <param name="format">The optional format to use -or- null to use the format information from the model, if available, or the default format for the type.</param>
		/// <param name="provider">The optional format provider to use.</param>
		/// <returns>The value of the expression, formatted as a string.</returns>
		public string GetFormattedValue(ModelInstance instance, string format, IFormatProvider provider)
		{
			var rawValue = Invoke(instance);

			if (rawValue == null)
				return "";

			// Assumes the value has already been formatted if the value is a string
			if (rawValue is string)
				return (string)rawValue;

			return Formatter.FormatResult(rawValue, format, provider);
		}

		#endregion

		#region IExpressionCompiler

		public interface IExpressionCompiler
		{
			LambdaExpression Compile(Expr expression, ModelParameterExpression root);
		}

		#endregion

		#region IModelTypeExpression

		public interface IExpressionType
		{
			ModelType ModelType { get; }

			Type Type { get; }

			bool IsList { get; }
		}

		#endregion

		#region ITimeZoneProvider

		public interface ITimeZoneProvider
		{
			TimeZoneInfo TimeZone { get; }
		}

		#endregion

		#region ModelExpressionType

		public class ModelExpressionType : IExpressionType
		{
			public ModelExpressionType(ModelType modelType, bool isList)
			{
				this.ModelType = modelType;
				this.Type = typeof(object);

				var type = modelType;
				while (type != null)
				{
					if (type is IReflectionModelType)
					{
						this.Type = ((IReflectionModelType)type).UnderlyingType;
						break;
					}
					type = type.BaseType;
				}

				this.Name = modelType.Name;
				this.IsList = isList;
			}

			public ModelExpressionType(Type type)
			{
				this.Type = type;
				this.Name = type == null ? "" : type.Name;
				this.IsList = false;
			}

			public ModelType ModelType { get; private set; }

			public Type Type { get; private set; }

			public string Name { get; private set; }

			public bool IsList { get; private set; }
		}

		#endregion

		#region ModelParameterExpression

		public class ModelParameterExpression : Expression, IExpressionType
		{
			public ModelParameterExpression(IExpressionType type, string name)
				: base(ExpressionType.Parameter, type.Type)
			{
				this.ModelType = type.ModelType;
				this.Name = name;
				this.IsList = type.IsList;
			}

			public ModelType ModelType { get; private set; }

			public string Name { get; private set; }

			public bool IsList { get; private set; }
		}

		#endregion

		#region ModelCastExpression

		public class ModelCastExpression : Expression, IExpressionType
		{
			public ModelCastExpression(MethodCallExpression expression, ModelType modelType, bool isList)
				: base(expression.NodeType, expression.Type)
			{
				this.Expression = expression;
				this.ModelType = modelType;
				this.IsList = isList;
			}

			public MethodCallExpression Expression { get; private set; }

			public ModelType ModelType { get; private set; }

			public bool IsList { get; private set; }
		}

		#endregion

		#region ModelLambdaExpression

		public class ModelLambdaExpression : Expression
		{
			public ModelLambdaExpression(Expression body, params ModelParameterExpression[] parameters)
				: base(ExpressionType.Lambda, GetDelegateType(body, parameters))
			{
				this.Body = body;
				this.Parameters = new ReadOnlyCollection<ModelParameterExpression>(parameters ?? new ModelParameterExpression[] { });
			}

			public Expression Body { get; private set; }

			public ReadOnlyCollection<ModelParameterExpression> Parameters { get; private set; }

			static Type GetDelegateType(Expression body, params ModelParameterExpression[] parameters)
			{
				parameters = parameters ?? new ModelParameterExpression[] { };
				return Expr.Lambda(body, parameters.Select(p => Expr.Parameter(p.Type, p.Name)).ToArray()).Type;
			}
		}

		#endregion

		#region ModelMemberExpression

		public class ModelMemberExpression : Expression, IExpressionType
		{
			public ModelMemberExpression(Expression expression, ModelProperty property)
				: base(ExpressionType.Call,
					property is ModelValueProperty ? ((ModelValueProperty)property).PropertyType :
					property is IReflectionModelType ?
						((IReflectionModelType)property).UnderlyingType :
						typeof(object))
			{
				this.Expression = expression;
				this.Property = property;
				this.ModelType = property is ModelReferenceProperty ? ((ModelReferenceProperty)property).PropertyType : null;
				this.IsList = property is ModelReferenceProperty ? ((ModelReferenceProperty)property).IsList : false;
			}

			public Expression Expression { get; private set; }

			public new ModelProperty Property { get; private set; }

			public ModelType ModelType { get; private set; }

			public bool IsList { get; private set; }
		}

		#endregion

		#region DynamicClass

		internal abstract class DynamicClass
		{
			public override string ToString()
			{
				PropertyInfo[] props = this.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
				StringBuilder sb = new StringBuilder();
				sb.Append("{");
				for (int i = 0; i < props.Length; i++)
				{
					if (i > 0) sb.Append(", ");
					sb.Append(props[i].Name);
					sb.Append("=");
					sb.Append(props[i].GetValue(this, null));
				}
				sb.Append("}");
				return sb.ToString();
			}
		}

		#endregion

		#region DynamicProperty

		internal class DynamicProperty
		{
			string name;
			Type type;

			public DynamicProperty(string name, Type type)
			{
				if (name == null) throw new ArgumentNullException("name");
				if (type == null) throw new ArgumentNullException("type");
				this.name = name;
				this.type = type;
			}

			public string Name
			{
				get { return name; }
			}

			public Type Type
			{
				get { return type; }
			}
		}

		#endregion

		#region DynamicOrdering

		internal class DynamicOrdering
		{
			public Expression Selector;
			public bool Ascending;
		}

		#endregion

		#region Signature

		internal class Signature : IEquatable<Signature>
		{
			public DynamicProperty[] properties;
			public int hashCode;

			public Signature(IEnumerable<DynamicProperty> properties)
			{
				this.properties = properties.ToArray();
				hashCode = 0;
				foreach (DynamicProperty p in properties)
				{
					hashCode ^= p.Name.GetHashCode() ^ p.Type.GetHashCode();
				}
			}

			public override int GetHashCode()
			{
				return hashCode;
			}

			public override bool Equals(object obj)
			{
				return obj is Signature ? Equals((Signature)obj) : false;
			}

			public bool Equals(Signature other)
			{
				if (properties.Length != other.properties.Length) return false;
				for (int i = 0; i < properties.Length; i++)
				{
					if (properties[i].Name != other.properties[i].Name ||
						properties[i].Type != other.properties[i].Type) return false;
				}
				return true;
			}
		}

		#endregion

		#region ClassFactory

		internal class ClassFactory
		{
			public static readonly ClassFactory Instance = new ClassFactory();

			static ClassFactory() { }  // Trigger lazy initialization of static fields

			ModuleBuilder module;
			Dictionary<Signature, Type> classes;
			int classCount;
			ReaderWriterLock rwLock;

			private ClassFactory()
			{
				AssemblyName name = new AssemblyName("DynamicClasses");
				AssemblyBuilder assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
#if ENABLE_LINQ_PARTIAL_TRUST
            new ReflectionPermission(PermissionState.Unrestricted).Assert();
#endif
				try
				{
					module = assembly.DefineDynamicModule("Module");
				}
				finally
				{
#if ENABLE_LINQ_PARTIAL_TRUST
                PermissionSet.RevertAssert();
#endif
				}
				classes = new Dictionary<Signature, Type>();
				rwLock = new ReaderWriterLock();
			}

			public Type GetDynamicClass(IEnumerable<DynamicProperty> properties)
			{
				rwLock.AcquireReaderLock(Timeout.Infinite);
				try
				{
					Signature signature = new Signature(properties);
					Type type;
					if (!classes.TryGetValue(signature, out type))
					{
						type = CreateDynamicClass(signature.properties);
						classes.Add(signature, type);
					}
					return type;
				}
				finally
				{
					rwLock.ReleaseReaderLock();
				}
			}

			Type CreateDynamicClass(DynamicProperty[] properties)
			{
				LockCookie cookie = rwLock.UpgradeToWriterLock(Timeout.Infinite);
				try
				{
					string typeName = "DynamicClass" + (classCount + 1);
#if ENABLE_LINQ_PARTIAL_TRUST
                new ReflectionPermission(PermissionState.Unrestricted).Assert();
#endif
					try
					{
						TypeBuilder tb = this.module.DefineType(typeName, TypeAttributes.Class |
							TypeAttributes.Public, typeof(DynamicClass));
						FieldInfo[] fields = GenerateProperties(tb, properties);
						GenerateEquals(tb, fields);
						GenerateGetHashCode(tb, fields);
						Type result = tb.CreateType();
						classCount++;
						return result;
					}
					finally
					{
#if ENABLE_LINQ_PARTIAL_TRUST
                    PermissionSet.RevertAssert();
#endif
					}
				}
				finally
				{
					rwLock.DowngradeFromWriterLock(ref cookie);
				}
			}

			FieldInfo[] GenerateProperties(TypeBuilder tb, DynamicProperty[] properties)
			{
				FieldInfo[] fields = new FieldBuilder[properties.Length];
				for (int i = 0; i < properties.Length; i++)
				{
					DynamicProperty dp = properties[i];
					FieldBuilder fb = tb.DefineField("_" + dp.Name, dp.Type, FieldAttributes.Private);
					PropertyBuilder pb = tb.DefineProperty(dp.Name, PropertyAttributes.HasDefault, dp.Type, null);
					MethodBuilder mbGet = tb.DefineMethod("get_" + dp.Name,
						MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
						dp.Type, Type.EmptyTypes);
					ILGenerator genGet = mbGet.GetILGenerator();
					genGet.Emit(OpCodes.Ldarg_0);
					genGet.Emit(OpCodes.Ldfld, fb);
					genGet.Emit(OpCodes.Ret);
					MethodBuilder mbSet = tb.DefineMethod("set_" + dp.Name,
						MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
						null, new Type[] { dp.Type });
					ILGenerator genSet = mbSet.GetILGenerator();
					genSet.Emit(OpCodes.Ldarg_0);
					genSet.Emit(OpCodes.Ldarg_1);
					genSet.Emit(OpCodes.Stfld, fb);
					genSet.Emit(OpCodes.Ret);
					pb.SetGetMethod(mbGet);
					pb.SetSetMethod(mbSet);
					fields[i] = fb;
				}
				return fields;
			}

			void GenerateEquals(TypeBuilder tb, FieldInfo[] fields)
			{
				MethodBuilder mb = tb.DefineMethod("Equals",
					MethodAttributes.Public | MethodAttributes.ReuseSlot |
					MethodAttributes.Virtual | MethodAttributes.HideBySig,
					typeof(bool), new Type[] { typeof(object) });
				ILGenerator gen = mb.GetILGenerator();
				LocalBuilder other = gen.DeclareLocal(tb);
				Label next = gen.DefineLabel();
				gen.Emit(OpCodes.Ldarg_1);
				gen.Emit(OpCodes.Isinst, tb);
				gen.Emit(OpCodes.Stloc, other);
				gen.Emit(OpCodes.Ldloc, other);
				gen.Emit(OpCodes.Brtrue_S, next);
				gen.Emit(OpCodes.Ldc_I4_0);
				gen.Emit(OpCodes.Ret);
				gen.MarkLabel(next);
				foreach (FieldInfo field in fields)
				{
					Type ft = field.FieldType;
					Type ct = typeof(EqualityComparer<>).MakeGenericType(ft);
					next = gen.DefineLabel();
					gen.EmitCall(OpCodes.Call, ct.GetMethod("get_Default"), null);
					gen.Emit(OpCodes.Ldarg_0);
					gen.Emit(OpCodes.Ldfld, field);
					gen.Emit(OpCodes.Ldloc, other);
					gen.Emit(OpCodes.Ldfld, field);
					gen.EmitCall(OpCodes.Callvirt, ct.GetMethod("Equals", new Type[] { ft, ft }), null);
					gen.Emit(OpCodes.Brtrue_S, next);
					gen.Emit(OpCodes.Ldc_I4_0);
					gen.Emit(OpCodes.Ret);
					gen.MarkLabel(next);
				}
				gen.Emit(OpCodes.Ldc_I4_1);
				gen.Emit(OpCodes.Ret);
			}

			void GenerateGetHashCode(TypeBuilder tb, FieldInfo[] fields)
			{
				MethodBuilder mb = tb.DefineMethod("GetHashCode",
					MethodAttributes.Public | MethodAttributes.ReuseSlot |
					MethodAttributes.Virtual | MethodAttributes.HideBySig,
					typeof(int), Type.EmptyTypes);
				ILGenerator gen = mb.GetILGenerator();
				gen.Emit(OpCodes.Ldc_I4_0);
				foreach (FieldInfo field in fields)
				{
					Type ft = field.FieldType;
					Type ct = typeof(EqualityComparer<>).MakeGenericType(ft);
					gen.EmitCall(OpCodes.Call, ct.GetMethod("get_Default"), null);
					gen.Emit(OpCodes.Ldarg_0);
					gen.Emit(OpCodes.Ldfld, field);
					gen.EmitCall(OpCodes.Callvirt, ct.GetMethod("GetHashCode", new Type[] { ft }), null);
					gen.Emit(OpCodes.Xor);
				}
				gen.Emit(OpCodes.Ret);
			}
		}

		#endregion

		#region ParseException

		public sealed class ParseException : Exception
		{
			static Dictionary<ParseErrorType, string> errorMessages = new Dictionary<ParseErrorType, string> 
            {
			    { ParseErrorType.DuplicateIdentifier, "The identifier '{0}' was defined more than once" },
			    { ParseErrorType.ExpressionTypeMismatch, "Expression of type '{0}' expected" },
			    { ParseErrorType.ExpressionExpected, "Expression expected" },
			    { ParseErrorType.InvalidCharacterLiteral, "Character literal must contain exactly one character" },
			    { ParseErrorType.InvalidIntegerLiteral, "Invalid integer literal '{0}'" },
			    { ParseErrorType.InvalidRealLiteral, "Invalid real literal '{0}'" },
			    { ParseErrorType.UnknownIdentifier, "Unknown identifier '{0}'" },
			    { ParseErrorType.NoItInScope, "No 'it' is in scope" },
			    { ParseErrorType.IifRequiresThreeArgs, "The 'iif' function requires three arguments" },
			    { ParseErrorType.FirstExprMustBeBool, "The first expression must be of type 'Boolean'" },
			    { ParseErrorType.BothTypesConvertToOther, "Both of the types '{0}' and '{1}' convert to the other" },
			    { ParseErrorType.NeitherTypeConvertsToOther, "Neither of the types '{0}' and '{1}' converts to the other" },
			    { ParseErrorType.MissingAsClause, "Expression is missing an 'as' clause" },
			    { ParseErrorType.ArgsIncompatibleWithLambda, "Argument list incompatible with lambda expression" },
			    { ParseErrorType.TypeHasNoNullableForm, "Type '{0}' has no nullable form" },
			    { ParseErrorType.NoMatchingConstructor, "No matching constructor in type '{0}'" },
			    { ParseErrorType.AmbiguousConstructorInvocation, "Ambiguous invocation of '{0}' constructor" },
			    { ParseErrorType.CannotConvertValue, "A value of type '{0}' cannot be converted to type '{1}'" },
			    { ParseErrorType.NoApplicableMethod, "No applicable method '{0}' exists in type '{1}'" },
			    { ParseErrorType.MethodsAreInaccessible, "Methods on type '{0}' are not accessible" },
			    { ParseErrorType.MethodIsVoid, "Method '{0}' in type '{1}' does not return a value" },
			    { ParseErrorType.AmbiguousMethodInvocation, "Ambiguous invocation of method '{0}' in type '{1}'" },
			    { ParseErrorType.UnknownPropertyOrField, "No property or field '{0}' exists in type '{1}'" },
			    { ParseErrorType.NoApplicableAggregate, "No applicable aggregate method '{0}' exists" },
			    { ParseErrorType.CannotIndexMultiDimArray, "Indexing of multi-dimensional arrays is not supported" },
			    { ParseErrorType.InvalidIndex, "Array index must be an integer expression" },
			    { ParseErrorType.NoApplicableIndexer, "No applicable indexer exists in type '{0}'" },
			    { ParseErrorType.AmbiguousIndexerInvocation, "Ambiguous invocation of indexer in type '{0}'" },
			    { ParseErrorType.IncompatibleOperand, "Operator '{0}' incompatible with operand type '{1}'" },
			    { ParseErrorType.IncompatibleOperands, "Operator '{0}' incompatible with operand types '{1}' and '{2}'" },
			    { ParseErrorType.UnterminatedStringLiteral, "Unterminated string literal" },
			    { ParseErrorType.InvalidCharacter, "Syntax error '{0}'" },
			    { ParseErrorType.DigitExpected, "Digit expected" },
			    { ParseErrorType.SyntaxError, "Syntax error" },
			    { ParseErrorType.TokenExpected, "{0} expected" },
			    { ParseErrorType.ColonExpected, "':' expected" },
			    { ParseErrorType.OpenParenExpected, "'(' expected" },
			    { ParseErrorType.CloseParenOrOperatorExpected, "')' or operator expected" },
			    { ParseErrorType.CloseParenOrCommaExpected, "')' or ',' expected" },
			    { ParseErrorType.DotOrOpenParenExpected, "'.' or '(' expected" },
			    { ParseErrorType.OpenBracketExpected, "'[' expected" },
			    { ParseErrorType.CloseBracketOrCommaExpected, "']' or ',' expected" },
			    { ParseErrorType.IdentifierExpected, "Identifier expected" },
				{ ParseErrorType.ThenExpected, "'then' expected" },
				{ ParseErrorType.ElseExpected, "'else' expected" },
		    };

			public ParseException(string message, int position, params object[] args)
				: base(message)
			{
				this.Position = position;
				this.Arguments = args;
			}

			public ParseException(ParseErrorType error, int position, params object[] args)
				: base(string.Format(System.Globalization.CultureInfo.CurrentCulture, errorMessages[error], args))
			{
				this.Error = error;
				this.Position = position;
				this.Arguments = args;
			}

			public int Position { get; private set; }

			public object[] Arguments { get; private set; }

			public ParseErrorType Error { get; private set; }

			public override string ToString()
			{
				return string.Format("{0} (at index {1})", Message, Position);
			}
		}

		#endregion

		#region IntelliSense

		/// <summary>
		/// Throw when parsing expressions in IntelliSense mode, to provide insight into the
		/// scope parameters at a specific point in an expression.
		/// </summary>
		public class IntelliSense
		{
			public int Position { get; internal set; }

			public IExpressionType Type { get; internal set; }

			public IntelliSenseScope Scope { get; internal set; }
		}

		#endregion

		#region IntelliSenseScope

		[Flags]
		public enum IntelliSenseScope
		{
			StaticMembers = 1,
			InstanceMembers = 2,
			Globals = 3
		}

		#endregion

		#region ParseErrorType

		public enum ParseErrorType
		{
			DuplicateIdentifier,
			ExpressionTypeMismatch,
			ExpressionExpected,
			InvalidCharacterLiteral,
			InvalidIntegerLiteral,
			InvalidRealLiteral,
			UnknownIdentifier,
			NoItInScope,
			IifRequiresThreeArgs,
			FirstExprMustBeBool,
			BothTypesConvertToOther,
			NeitherTypeConvertsToOther,
			MissingAsClause,
			ArgsIncompatibleWithLambda,
			TypeHasNoNullableForm,
			NoMatchingConstructor,
			AmbiguousConstructorInvocation,
			CannotConvertValue,
			NoApplicableMethod,
			MethodsAreInaccessible,
			MethodIsVoid,
			AmbiguousMethodInvocation,
			UnknownPropertyOrField,
			NoApplicableAggregate,
			CannotIndexMultiDimArray,
			InvalidIndex,
			NoApplicableIndexer,
			AmbiguousIndexerInvocation,
			IncompatibleOperand,
			IncompatibleOperands,
			UnterminatedStringLiteral,
			InvalidCharacter,
			DigitExpected,
			SyntaxError,
			TokenExpected,
			ParseExceptionFormat,
			ColonExpected,
			OpenParenExpected,
			CloseParenOrOperatorExpected,
			CloseParenOrCommaExpected,
			DotOrOpenParenExpected,
			OpenBracketExpected,
			CloseBracketOrCommaExpected,
			IdentifierExpected,
			ThenExpected,
			ElseExpected
		}

		#endregion

		#region ExpressionParser

		public class ExpressionParser
		{
			public delegate void MaxDepthChangedHandler(int depth);
			public event MaxDepthChangedHandler MaxDepthChanged;

			struct Token
			{
				public TokenId id;
				public string text;
				public int pos;
			}

			enum TokenId
			{
				Unknown,
				End,
				Identifier,
				StringLiteral,
				IntegerLiteral,
				RealLiteral,
				Exclamation,
				Percent,
				Amphersand,
				OpenParen,
				CloseParen,
				Asterisk,
				Plus,
				Comma,
				Minus,
				Dot,
				Slash,
				Colon,
				LessThan,
				Equal,
				GreaterThan,
				Question,
				OpenBracket,
				CloseBracket,
				Bar,
				ExclamationEqual,
				DoubleAmphersand,
				LessThanEqual,
				LessGreater,
				DoubleEqual,
				GreaterThanEqual,
				DoubleBar,
				If,
				Then,
				Else
			}

			interface ILogicalSignatures
			{
				void F(bool x, bool y);
				void F(bool? x, bool? y);
			}

			interface IArithmeticSignatures
			{
				void F(int x, int y);
				void F(uint x, uint y);
				void F(long x, long y);
				void F(ulong x, ulong y);
				void F(float x, float y);
				void F(double x, double y);
				void F(decimal x, decimal y);
				void F(int? x, int? y);
				void F(uint? x, uint? y);
				void F(long? x, long? y);
				void F(ulong? x, ulong? y);
				void F(float? x, float? y);
				void F(double? x, double? y);
				void F(decimal? x, decimal? y);
			}

			interface IRelationalSignatures : IArithmeticSignatures
			{

				void F(char x, char y);
				void F(DateTime x, DateTime y);
				void F(TimeSpan x, TimeSpan y);
				void F(char? x, char? y);
				void F(DateTime? x, DateTime? y);
				void F(TimeSpan? x, TimeSpan? y);
				void F(string x, string y);
			}

			interface IEqualitySignatures : IRelationalSignatures
			{
				void F(bool x, bool y);
				void F(bool? x, bool? y);
			}

			interface IAddSignatures : IArithmeticSignatures
			{
				void F(DateTime x, TimeSpan y);
				void F(TimeSpan x, TimeSpan y);
				void F(DateTime? x, TimeSpan? y);
				void F(TimeSpan? x, TimeSpan? y);
			}

			interface ISubtractSignatures : IAddSignatures
			{
				void F(DateTime x, DateTime y);
				void F(DateTime? x, DateTime? y);
			}

			interface INegationSignatures
			{
				void F(int x);
				void F(long x);
				void F(float x);
				void F(double x);
				void F(decimal x);
				void F(int? x);
				void F(long? x);
				void F(float? x);
				void F(double? x);
				void F(decimal? x);
			}

			interface INotSignatures
			{
				void F(bool x);
				void F(bool? x);
			}

			interface IEnumerableSignatures
			{
				void First(bool predicate);
				void FirstOrDefault(bool predicate);
				void First();
				void FirstOrDefault();
				void Last(bool predicate);
				void LastOrDefault(bool predicate);
				void Last();
				void LastOrDefault();
				void Where(bool predicate);
				void Any();
				void Any(bool predicate);
				void All(bool predicate);
				void Contains(object value);
				void Count();
				void Count(bool predicate);
				void Min(object selector);
				void Max(object selector);
				void Sum(int selector);
				void Sum(int? selector);
				void Sum(long selector);
				void Sum(long? selector);
				void Sum(float selector);
				void Sum(float? selector);
				void Sum(double selector);
				void Sum(double? selector);
				void Sum(decimal selector);
				void Sum(decimal? selector);
				void Average(int selector);
				void Average(int? selector);
				void Average(long selector);
				void Average(long? selector);
				void Average(float selector);
				void Average(float? selector);
				void Average(double selector);
				void Average(double? selector);
				void Average(decimal selector);
				void Average(decimal? selector);
				void Select(object selector);
				void OrderBy(object selector);
				void OrderByDescending(object selector);
				void Except(object items);

				//void Max();
				//void Min();
				//void AsEnumerable();
				//void Aggregate(object func);
				//void Average();
				//void Cast(???);
				//void Concat(object items);
				//void DefaultIfEmpty();
				//void DefaultIfEmpty(object value);
				//void Distinct();
				//void Distinct(object comparer);
				//void ElementAt(int index);
				//void ElementAtOrDefault(int index);
				//void GroupBy(object selector);
				//void Intersect(object selector);
				//void LongCount();
				//void LongCount(bool predicate);
				//void OfType(???);
				//void Reverse();
				//void SelectMany(object selector);
				//void SequenceEqual(object items);
				//void Single();
				//void Single(bool predicate);
				//void SingleOrDefault();
				//void SingleOrDefault(bool predicate);
				//void Skip(int selector);
				//void SkipWhile(bool predicate);
				//void Sum();
				//void Take(int selector);
				//void TakeWhile(bool predicate);
				//void ToDictionary(object selector);
				//void ToLookup(object selector);
				//void Union(object items);
			}

			static readonly Type[] predefinedTypes = {
            typeof(Object),
            typeof(Boolean),
            typeof(Char),
            typeof(String),
            typeof(SByte),
            typeof(Byte),
            typeof(Int16),
            typeof(UInt16),
            typeof(Int32),
            typeof(UInt32),
            typeof(Int64),
            typeof(UInt64),
            typeof(Single),
            typeof(Double),
            typeof(Decimal),
            typeof(DateTime),
            typeof(TimeSpan),
            typeof(Guid),
            typeof(Math),
            typeof(Convert)
        };

			static readonly Expression trueLiteral = Expr.Constant(true);
			static readonly Expression falseLiteral = Expr.Constant(false);
			static readonly Expression nullLiteral = Expr.Constant(null);

			static readonly string keywordIt = "it";
			static readonly string keywordIif = "iif";
			static readonly string keywordNew = "new";

			static Dictionary<string, object> keywords;

			Dictionary<string, object> symbols;
			IDictionary<string, object> externals;
			Dictionary<Expression, string> literals;
			ModelParameterExpression it;
			string text;
			int textPos;
			int textLen;
			char ch;
			int depth;
			int maxDepth;
			Token token;
			Token prevToken;
			QuerySyntax querySyntax;

			internal IntelliSense IntelliSense { get; private set; }

			public Action<ModelMemberExpression, int> RenameExpression { get; set; }

			public ExpressionParser(ModelParameterExpression[] parameters, string expression, QuerySyntax querySyntax, params object[] values)
			{
				if (expression == null) throw new ArgumentNullException("expression");
				if (keywords == null) keywords = CreateKeywords();
				symbols = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
				literals = new Dictionary<Expression, string>();
				if (parameters != null) ProcessParameters(parameters);
				if (values != null) ProcessValues(values);
				text = expression;
				textLen = text.Length;
				SetTextPos(0);

				// Initialize IntelliSense
				this.IntelliSense = new IntelliSense() { Position = 0, Type = it, Scope = it == null ? IntelliSenseScope.Globals : IntelliSenseScope.Globals | IntelliSenseScope.InstanceMembers };

				NextToken();
				this.querySyntax = querySyntax;
			}

			public ExpressionParser(ModelParameterExpression[] parameters, string expression, params object[] values)
				: this(parameters, expression, QuerySyntax.DotNet, values)
			{ }

			void ProcessParameters(ModelParameterExpression[] parameters)
			{
				foreach (ModelParameterExpression pe in parameters)
					if (!String.IsNullOrEmpty(pe.Name))
						AddSymbol(pe.Name, pe);
				if (parameters.Length == 1 && String.IsNullOrEmpty(parameters[0].Name))
					it = parameters[0];
			}

			void ProcessValues(object[] values)
			{
				for (int i = 0; i < values.Length; i++)
				{
					object value = values[i];
					if (i == values.Length - 1 && value is IDictionary<string, object>)
					{
						externals = (IDictionary<string, object>)value;
					}
					else
					{
						AddSymbol("@" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), value);
					}
				}
			}

			void AddSymbol(string name, object value)
			{
				if (symbols.ContainsKey(name))
					throw ParseError(ParseErrorType.DuplicateIdentifier, name);
				symbols.Add(name, value);
			}

			public Expression Parse(ModelExpressionType resultType)
			{
				int exprPos = token.pos;
				Expression expr = ParseExpression(resultType != null ? resultType.Type : null);
				if (resultType != null)
				{
					var promotedExpression = PromoteExpression(expr, resultType.Type, true);
					if (promotedExpression == null)
						throw ParseError(exprPos, ParseErrorType.ExpressionTypeMismatch, GetTypeName(resultType));
					expr = promotedExpression;
				}

				ValidateToken(TokenId.End, ParseErrorType.SyntaxError);

				// No parse errors, could still provide IntelliSense (i.e. Person.FirstName vs Person.FirstChoice)
				IntelliSense.Position = token.pos - prevToken.text.Length;

				return expr;
			}

			// ?: operator
			Expression ParseExpression(Type resultType = null)
			{
				depth++;
				int errorPos = token.pos;
				Expression expr = ParseIf(resultType);
				if (token.id == TokenId.Question)
				{
					NextToken();
					Expression expr1 = ParseExpression(resultType);
					if (resultType != null)
					{
						var type = expr1.Type;
						expr1 = PromoteExpression(expr1, resultType, true);
						if (expr1 == null)
							throw ParseError(ParseErrorType.CannotConvertValue, GetTypeName(new ModelExpressionType(type)), GetTypeName(new ModelExpressionType(resultType)));
					}
					ValidateToken(TokenId.Colon, ParseErrorType.ColonExpected);
					NextToken();
					Expression expr2 = ParseExpression(resultType);
					if (resultType != null)
					{
						var type = expr2.Type;
						expr2 = PromoteExpression(expr2, resultType, true);
						if (expr2 == null)
							throw ParseError(ParseErrorType.CannotConvertValue, GetTypeName(new ModelExpressionType(type)), GetTypeName(new ModelExpressionType(resultType)));
					}
					expr = GenerateConditional(expr, expr1, expr2, errorPos);
				}
				if (depth > maxDepth)
				{
					maxDepth = depth;
					if (MaxDepthChanged != null)
						MaxDepthChanged(maxDepth);
				}
				depth--;

				return expr;
			}

			// If Then Else operator
			Expression ParseIf(Type resultType = null)
			{
				int errorPos = token.pos;
				if (token.id == TokenId.If)
				{
					NextToken();
					Expression ifExpr = ParseExpression(typeof(bool));
					if (resultType != null)
					{
						ifExpr = PromoteExpression(ifExpr, typeof(bool), true);
						if (ifExpr == null)
							throw ParseError(ParseErrorType.FirstExprMustBeBool);
					}
					ValidateToken(TokenId.Then, ParseErrorType.ThenExpected);
					NextToken();
					Expression expr1 = ParseExpression(resultType);
					if (resultType != null)
					{
						var type = expr1.Type;
						expr1 = PromoteExpression(expr1, resultType, true);
						if (expr1 == null)
							throw ParseError(ParseErrorType.CannotConvertValue, GetTypeName(new ModelExpressionType(type)), GetTypeName(new ModelExpressionType(resultType)));
					}
					ValidateToken(TokenId.Else, ParseErrorType.ElseExpected);
					NextToken();
					Expression expr2 = ParseExpression(resultType);
					if (resultType != null)
					{
						var type = expr2.Type;
						expr2 = PromoteExpression(expr2, resultType, true);
						if (expr2 == null)
							throw ParseError(ParseErrorType.CannotConvertValue, GetTypeName(new ModelExpressionType(type)), GetTypeName(new ModelExpressionType(resultType)));
					}
					return GenerateConditional(ifExpr, expr1, expr2, errorPos);
				}
				else
					return ParseLogicalOr();
			}

			// ||, or operator
			Expression ParseLogicalOr()
			{
				Expression left = ParseLogicalAnd();
				while (token.id == TokenId.DoubleBar || TokenIdentifierIs("or"))
				{
					Token op = token;
					NextToken();
					Expression right = ParseLogicalAnd();
					CheckAndPromoteOperands(typeof(ILogicalSignatures), op.text, ref left, ref right, op.pos);
					left = Expr.OrElse(left, right);
				}
				return left;
			}

			// &&, and operator
			Expression ParseLogicalAnd()
			{
				Expression left = ParseComparison();
				while (token.id == TokenId.DoubleAmphersand || TokenIdentifierIs("and"))
				{
					Token op = token;
					NextToken();
					Expression right = ParseComparison();
					CheckAndPromoteOperands(typeof(ILogicalSignatures), op.text, ref left, ref right, op.pos);
					left = Expr.AndAlso(left, right);
				}
				return left;
			}

			// =, ==, !=, <>, >, >=, <, <= operators
			Expression ParseComparison()
			{
				Expression left = ParseAdditive();
				while (token.id == TokenId.Equal || token.id == TokenId.DoubleEqual || TokenIdentifierIs("eq") || TokenIdentifierIs("ne") ||
					token.id == TokenId.ExclamationEqual || token.id == TokenId.LessGreater ||
					token.id == TokenId.GreaterThan || token.id == TokenId.GreaterThanEqual ||
					TokenIdentifierIs("gt") || TokenIdentifierIs("ge") ||
					token.id == TokenId.LessThan || token.id == TokenId.LessThanEqual ||
					TokenIdentifierIs("lt") || TokenIdentifierIs("le"))
				{
					Token op = token;
					NextToken();
					Expression right = ParseAdditive();
					bool isEquality = op.id == TokenId.Equal || op.id == TokenId.DoubleEqual || TokenIdentifierIs(op, "eq") || TokenIdentifierIs(op, "ne") ||
						op.id == TokenId.ExclamationEqual || op.id == TokenId.LessGreater;
					if (isEquality && !left.Type.IsValueType && !right.Type.IsValueType)
					{
						if (left.Type != right.Type)
						{
							if (left.Type.IsAssignableFrom(right.Type))
							{
								right = Expr.Convert(right, left.Type);
							}
							else if (right.Type.IsAssignableFrom(left.Type))
							{
								left = Expr.Convert(left, right.Type);
							}
							else
							{
								throw IncompatibleOperandsError(op.text, left, right, op.pos);
							}
						}
					}
					else if (IsEnumType(left.Type) || IsEnumType(right.Type))
					{
						if (left.Type != right.Type)
						{
							Expression e;
							if ((e = PromoteExpression(right, left.Type, true)) != null)
							{
								right = e;
							}
							else if ((e = PromoteExpression(left, right.Type, true)) != null)
							{
								left = e;
							}
							else
							{
								throw IncompatibleOperandsError(op.text, left, right, op.pos);
							}
						}
					}
					else if (querySyntax == QuerySyntax.OData && (left == trueLiteral || left == falseLiteral || right == trueLiteral || right == falseLiteral))
					{
						// Remove unnecessary boolean condition (string.StartsWith(str) eq true)
						if (left == trueLiteral || left == falseLiteral)
							// true eq exp | false ne exp
							return (left == trueLiteral && TokenIdentifierIs(op, "eq")) || (left == falseLiteral && TokenIdentifierIs(op, "ne")) ? right : Expr.Not(right);
						else
							// exp eq true | exp ne false
							return (right == trueLiteral && TokenIdentifierIs(op, "eq")) || (right == falseLiteral && TokenIdentifierIs(op, "ne")) ? left : Expr.Not(left);
					}
					else
					{
						CheckAndPromoteOperands(isEquality ? typeof(IEqualitySignatures) : typeof(IRelationalSignatures),
							op.text, ref left, ref right, op.pos);
					}

					if (querySyntax == QuerySyntax.OData)
					{
						if (TokenIdentifierIs(op, "eq"))
							left = GenerateEqual(left, right);
						else if (TokenIdentifierIs(op, "ne"))
							left = GenerateNotEqual(left, right);
						else if (TokenIdentifierIs(op, "gt"))
							left = GenerateGreaterThan(left, right);
						else if (TokenIdentifierIs(op, "ge"))
							left = GenerateGreaterThanEqual(left, right);
						else if (TokenIdentifierIs(op, "lt"))
							left = GenerateLessThan(left, right);
						else if (TokenIdentifierIs(op, "le"))
							left = GenerateLessThanEqual(left, right);
					}
					else
					{
						switch (op.id)
						{
							case TokenId.Equal:
							case TokenId.DoubleEqual:
								left = GenerateEqual(left, right);
								break;
							case TokenId.ExclamationEqual:
							case TokenId.LessGreater:
								left = GenerateNotEqual(left, right);
								break;
							case TokenId.GreaterThan:
								left = GenerateGreaterThan(left, right);
								break;
							case TokenId.GreaterThanEqual:
								left = GenerateGreaterThanEqual(left, right);
								break;
							case TokenId.LessThan:
								left = GenerateLessThan(left, right);
								break;
							case TokenId.LessThanEqual:
								left = GenerateLessThanEqual(left, right);
								break;
						}
					}
				}
				return left;
			}

			// +, -, & operators
			Expression ParseAdditive()
			{
				Expression left = ParseMultiplicative();
				while (token.id == TokenId.Plus || token.id == TokenId.Minus ||
					token.id == TokenId.Amphersand || TokenIdentifierIs("Add") || TokenIdentifierIs("Sub"))
				{
					Token op = token;
					NextToken();
					Expression right = ParseMultiplicative();
					if (querySyntax == QuerySyntax.OData)
					{
						if (TokenIdentifierIs(op, "Add"))
						{
							CheckAndPromoteOperands(typeof(IAddSignatures), op.text, ref left, ref right, op.pos);
							left = GenerateAdd(left, right);
						}
						else if (TokenIdentifierIs(op, "Sub"))
						{
							CheckAndPromoteOperands(typeof(ISubtractSignatures), op.text, ref left, ref right, op.pos);
							left = GenerateSubtract(left, right);
						}
					}
					else
					{
						switch (op.id)
						{
							case TokenId.Plus:
								if (left.Type == typeof(string) || right.Type == typeof(string))
									goto case TokenId.Amphersand;
								CheckAndPromoteOperands(typeof(IAddSignatures), op.text, ref left, ref right, op.pos);
								left = GenerateAdd(left, right);
								break;
							case TokenId.Minus:
								CheckAndPromoteOperands(typeof(ISubtractSignatures), op.text, ref left, ref right, op.pos);
								left = GenerateSubtract(left, right);
								break;
							case TokenId.Amphersand:
								left = GenerateStringConcat(left, right);
								break;
						}
					}
				}
				return left;
			}

			// *, /, %, mod operators
			Expression ParseMultiplicative()
			{
				Expression left = ParseUnary();
				while (token.id == TokenId.Asterisk || token.id == TokenId.Slash ||
					token.id == TokenId.Percent || TokenIdentifierIs("mod") || TokenIdentifierIs("Mul") || TokenIdentifierIs("Div"))
				{
					Token op = token;
					NextToken();
					Expression right = ParseUnary();
					CheckAndPromoteOperands(typeof(IArithmeticSignatures), op.text, ref left, ref right, op.pos);
					if (querySyntax == QuerySyntax.OData)
					{
						if (TokenIdentifierIs(op, "Mul"))
							left = Expr.Multiply(left, right);
						else if (TokenIdentifierIs(op, "Div"))
							left = Expr.Divide(left, right);
						else if (TokenIdentifierIs(op, "Mod"))
							left = Expr.Modulo(left, right);
					}
					else
					{
						switch (op.id)
						{
							case TokenId.Asterisk:
								left = Expr.Multiply(left, right);
								break;
							case TokenId.Slash:
								left = Expr.Divide(Expr.Convert(left, typeof(double)), Expr.Convert(right, typeof(double)));
								break;
							case TokenId.Percent:
							case TokenId.Identifier:
								left = Expr.Modulo(left, right);
								break;
						}
					}
				}
				return left;
			}

			// -, !, not unary operators
			Expression ParseUnary()
			{
				if (token.id == TokenId.Minus || token.id == TokenId.Exclamation ||
					TokenIdentifierIs("not"))
				{
					Token op = token;
					NextToken();
					if (op.id == TokenId.Minus && (token.id == TokenId.IntegerLiteral ||
						token.id == TokenId.RealLiteral))
					{
						token.text = "-" + token.text;
						token.pos = op.pos;
						return ParsePrimary();
					}
					Expression expr = ParseUnary();
					if (op.id == TokenId.Minus)
					{
						CheckAndPromoteOperand(typeof(INegationSignatures), op.text, ref expr, op.pos);
						expr = Expr.Negate(expr);
					}
					else
					{
						CheckAndPromoteOperand(typeof(INotSignatures), op.text, ref expr, op.pos);
						expr = Expr.Not(expr);
					}
					return expr;
				}
				return ParsePrimary();
			}

			Expression ParsePrimary()
			{
				Expression expr = ParsePrimaryStart();
				while (true)
				{
					if ((querySyntax == QuerySyntax.DotNet && token.id == TokenId.Dot) || (querySyntax == QuerySyntax.OData && token.id == TokenId.Slash))
					{
						NextToken();
						expr = ParseMemberAccess(null, expr);
					}
					else if (token.id == TokenId.OpenBracket)
					{
						expr = ParseElementAccess(expr);
					}
					else
					{
						break;
					}
				}
				return expr;
			}

			Expression ParsePrimaryStart()
			{
				switch (token.id)
				{
					case TokenId.Identifier:
						return ParseIdentifier();
					case TokenId.StringLiteral:
						return ParseStringLiteral();
					case TokenId.IntegerLiteral:
						return ParseIntegerLiteral();
					case TokenId.RealLiteral:
						return ParseRealLiteral();
					case TokenId.OpenParen:
						return ParseParenExpression();
					case TokenId.OpenBracket:
						return ParseArrayLiteral();
					default:
						IntelliSense.Type = (it == null ? null : (it as IExpressionType) ?? new ModelExpressionType(it.Type));
						IntelliSense.Position = token.pos;
						IntelliSense.Scope = it == null ? IntelliSenseScope.Globals : IntelliSenseScope.Globals | IntelliSenseScope.InstanceMembers;

						throw ParseError(ParseErrorType.ExpressionExpected);
				}
			}

			Expression ParseStringLiteral()
			{
				ValidateToken(TokenId.StringLiteral);
				char quote = token.text[0];

				// In OData, strings are enclosed with single quotes
				if (querySyntax == QuerySyntax.OData && quote == '"')
					throw ParseError(ParseErrorType.SyntaxError);

				string s = token.text.Substring(1, token.text.Length - 2);
				int start = 0;
				while (true)
				{
					int i = s.IndexOf(quote, start);
					if (i < 0) break;
					s = s.Remove(i, 1);
					start = i + 1;
				}
				if (querySyntax == QuerySyntax.DotNet && quote == '\'')
				{
					if (s.Length != 1)
						throw ParseError(ParseErrorType.InvalidCharacterLiteral);
					NextToken();
					return CreateLiteral(s[0], s);
				}
				NextToken();
				return CreateLiteral(s, s);
			}

			Expression ParseIntegerLiteral()
			{
				ValidateToken(TokenId.IntegerLiteral);
				string text = token.text;
				if (text[0] != '-')
				{
					ulong value;
					if (!UInt64.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
						throw ParseError(ParseErrorType.InvalidIntegerLiteral, text);
					NextToken();
					if (value <= (ulong)Int32.MaxValue) return CreateLiteral((int)value, text);
					if (value <= (ulong)UInt32.MaxValue) return CreateLiteral((uint)value, text);
					if (value <= (ulong)Int64.MaxValue) return CreateLiteral((long)value, text);
					return CreateLiteral(value, text);
				}
				else
				{
					long value;
					if (!Int64.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
						throw ParseError(ParseErrorType.InvalidIntegerLiteral, text);
					NextToken();
					if (value >= Int32.MinValue && value <= Int32.MaxValue)
						return CreateLiteral((int)value, text);
					return CreateLiteral(value, text);
				}
			}

			Expression ParseRealLiteral()
			{
				ValidateToken(TokenId.RealLiteral);
				string text = token.text;
				object value = null;
				char last = text[text.Length - 1];
				if (last == 'F' || last == 'f')
				{
					float f;
					if (Single.TryParse(text.Substring(0, text.Length - 1), NumberStyles.Any, CultureInfo.InvariantCulture, out f)) value = f;
				}
				else
				{
					double d;
					if (Double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out d)) value = d;
				}
				if (value == null) throw ParseError(ParseErrorType.InvalidRealLiteral, text);
				NextToken();
				return CreateLiteral(value, text);
			}

			Expression CreateLiteral(object value, string text)
			{
				ConstantExpression expr = Expr.Constant(value);
				literals.Add(expr, text);
				return expr;
			}

			Expression ParseParenExpression()
			{
				ValidateToken(TokenId.OpenParen, ParseErrorType.OpenParenExpected);
				NextToken();
				Expression e = ParseExpression();
				ValidateToken(TokenId.CloseParen, ParseErrorType.CloseParenOrOperatorExpected);
				NextToken();
				return e;
			}

			Expression ParseIdentifier()
			{
				ValidateToken(TokenId.Identifier);
				object value;
				if (keywords.TryGetValue(token.text, out value))
				{
					if (value is Type) return ParseTypeAccess(new ModelExpressionType((Type)value));
					if (value == (object)keywordIt) return ParseIt();
					if (value == (object)keywordIif) return ParseIif();
					if (value == (object)keywordNew) return ParseNew();
					NextToken();
					return (Expression)value;
				}
				if (symbols.TryGetValue(token.text, out value) ||
					externals != null && externals.TryGetValue(token.text, out value))
				{
					Expression expr = value as Expression;
					if (expr == null)
					{
						expr = Expr.Constant(value);
					}
					else
					{
						LambdaExpression lambda = expr as LambdaExpression;
						if (lambda != null) return ParseLambdaInvocation(lambda);
					}
					NextToken();
					return expr;
				}
				if (it != null) return ParseMemberAccess(null, it);

				// Update IntelliSense
				IntelliSense.Type = null;
				IntelliSense.Scope = IntelliSenseScope.Globals;
				IntelliSense.Position = token.pos;

				throw ParseError(ParseErrorType.UnknownIdentifier, token.text);
			}

			Expression ParseIt()
			{
				if (it == null)
					throw ParseError(ParseErrorType.NoItInScope);
				NextToken();
				return it;
			}

			Expression ParseIif()
			{
				int errorPos = token.pos;
				NextToken();
				Expression[] args = ParseArgumentList();
				if (args.Length != 3)
					throw ParseError(errorPos, ParseErrorType.IifRequiresThreeArgs);
				return GenerateConditional(args[0], args[1], args[2], errorPos);
			}

			Expression GenerateConditional(Expression test, Expression expr1, Expression expr2, int errorPos)
			{
				if (test.Type != typeof(bool))
					throw ParseError(errorPos, ParseErrorType.FirstExprMustBeBool);
				if (expr1.Type != expr2.Type)
				{
					Expression expr1as2 = expr2 != nullLiteral || (expr1.Type.IsValueType && !(IsNullableType(expr1.Type))) ? PromoteExpression(expr1, expr2.Type, true) : null;
					Expression expr2as1 = expr1 != nullLiteral || (expr2.Type.IsValueType && !(IsNullableType(expr2.Type))) ? PromoteExpression(expr2, expr1.Type, true) : null;
					if (expr1as2 != null && expr2as1 == null)
					{
						expr1 = expr1as2;
					}
					else if (expr2as1 != null && expr1as2 == null)
					{
						expr2 = expr2as1;
					}
					else
					{
						string type1 = expr1 != nullLiteral ? expr1.Type.Name : "null";
						string type2 = expr2 != nullLiteral ? expr2.Type.Name : "null";
						if (expr1as2 != null && expr2as1 != null)
							throw ParseError(errorPos, ParseErrorType.BothTypesConvertToOther, type1, type2);
						throw ParseError(errorPos, ParseErrorType.NeitherTypeConvertsToOther, type1, type2);
					}
				}
				return Expr.Condition(test, expr1, expr2);
			}

			Expression ParseNew()
			{
				NextToken();
				ValidateToken(TokenId.OpenParen, ParseErrorType.OpenParenExpected);
				NextToken();
				List<DynamicProperty> properties = new List<DynamicProperty>();
				List<Expression> expressions = new List<Expression>();
				while (true)
				{
					int exprPos = token.pos;
					Expression expr = ParseExpression();
					string propName;
					if (TokenIdentifierIs("as"))
					{
						NextToken();
						propName = GetIdentifier();
						NextToken();
					}
					else
					{
						MemberExpression me = expr as MemberExpression;
						if (me == null) throw ParseError(exprPos, ParseErrorType.MissingAsClause);
						propName = me.Member.Name;
					}
					expressions.Add(expr);
					properties.Add(new DynamicProperty(propName, expr.Type));
					if (token.id != TokenId.Comma) break;
					NextToken();
				}
				ValidateToken(TokenId.CloseParen, ParseErrorType.CloseParenOrCommaExpected);
				NextToken();
				Type type = ModelExpression.CreateClass(properties);
				MemberBinding[] bindings = new MemberBinding[properties.Count];
				for (int i = 0; i < bindings.Length; i++)
					bindings[i] = Expr.Bind(type.GetProperty(properties[i].Name), expressions[i]);
				return Expr.MemberInit(Expr.New(type), bindings);
			}

			Expression ParseLambdaInvocation(LambdaExpression lambda)
			{
				int errorPos = token.pos;
				NextToken();
				Expression[] args = ParseArgumentList();
				MethodBase method;
				if (FindMethod(lambda.Type, "Invoke", false, ref args, out method) != 1)
					throw ParseError(errorPos, ParseErrorType.ArgsIncompatibleWithLambda);
				return Expr.Invoke(lambda, args);
			}

			Expression ParseTypeAccess(IExpressionType type)
			{
				int errorPos = token.pos;
				NextToken();
				if (token.id == TokenId.Question)
				{
					if (!type.Type.IsValueType || IsNullableType(type.Type))
						throw ParseError(errorPos, ParseErrorType.TypeHasNoNullableForm, GetTypeName(type));
					type = new ModelExpressionType(typeof(Nullable<>).MakeGenericType(type.Type));
					NextToken();
				}
				if (token.id == TokenId.OpenParen)
				{
					Expression[] args = ParseArgumentList();
					MethodBase method;
					switch (FindBestMethod(type.Type.GetConstructors(), ref args, out method))
					{
						case 0:
							if (args.Length == 1)
								return GenerateConversion(args[0], type.Type, errorPos);
							throw ParseError(errorPos, ParseErrorType.NoMatchingConstructor, GetTypeName(type));
						case 1:
							return Expr.New((ConstructorInfo)method, args);
						default:
							throw ParseError(errorPos, ParseErrorType.AmbiguousConstructorInvocation, GetTypeName(type));
					}
				}

				ValidateToken(TokenId.Dot, ParseErrorType.DotOrOpenParenExpected);
				NextToken();

				return ParseMemberAccess(type, null);
			}

			Expression GenerateConversion(Expression expr, Type type, int errorPos)
			{
				Type exprType = expr.Type;
				if (exprType == type) return expr;
				if (exprType.IsValueType && type.IsValueType)
				{
					if ((IsNullableType(exprType) || IsNullableType(type)) &&
						GetNonNullableType(exprType) == GetNonNullableType(type))
						return Expr.Convert(expr, type);
					if ((IsNumericType(exprType) || IsEnumType(exprType)) &&
						(IsNumericType(type)) || IsEnumType(type))
						return Expr.ConvertChecked(expr, type);
				}

				if (exprType.IsAssignableFrom(type) || type.IsAssignableFrom(exprType) ||
					exprType.IsInterface || type.IsInterface)
					return Expr.Convert(expr, type);
				throw ParseError(errorPos, ParseErrorType.CannotConvertValue,
					GetTypeName(expr), GetTypeName(new ModelExpressionType(type)));
			}

			Expression ParseMemberAccess(IExpressionType type, Expression instance)
			{
				// Coerce nullable expressions to their non-nullable equivalents by accessing the Value property.
				if (instance != null && IsNullableType(instance.Type))
					instance = Expr.MakeMemberAccess(instance, instance.Type.GetMember("Value")[0]);

				// Coerce null string to an empty string
				if (instance != null && instance.Type == typeof(string))
					instance = Expr.Coalesce(instance, Expr.Constant(""));

				// Update IntelliSense
				IntelliSense.Position = token.pos;
				if (instance != null && instance == it)
					IntelliSense.Scope = IntelliSenseScope.Globals;
				else if (instance != null)
					IntelliSense.Scope = IntelliSenseScope.InstanceMembers;
				else
					IntelliSense.Scope = IntelliSenseScope.StaticMembers;
				IntelliSense.Type = instance == null ? type : (instance as IExpressionType) ?? new ModelExpressionType(instance.Type);

				if (instance != null) type = (instance as IExpressionType) ?? new ModelExpressionType(instance.Type);

				int errorPos = token.pos;

				string id = GetIdentifier();
				NextToken();
				// Parse method
				if (token.id == TokenId.OpenParen)
				{
					if (instance != null && type.Type != typeof(string))
					{
						if (type.ModelType != null)
						{
							if (type.IsList)
							{
								return ParseAggregate(instance, new ModelExpressionType(type.ModelType, false), id, errorPos);
							}
						}
						else
						{
							Type enumerableType = FindGenericType(typeof(IEnumerable<>), type.Type);
							if (enumerableType != null)
							{
								Type elementType = enumerableType.GetGenericArguments()[0];
								return ParseAggregate(instance, new ModelExpressionType(elementType), id, errorPos);
							}
						}
					}
					Expression[] args = ParseArgumentList();
					MethodBase mb;

					switch (FindMethod(type.Type, id, instance == null, ref args, out mb))
					{
						case 0:
							if (querySyntax == QuerySyntax.OData)
							{
								switch (id)
								{
									case "substringof":
										if (args.Length == 2 && args[0].Type == typeof(string) && args[1].Type == typeof(string))
											return Expr.Call(args[0], typeof(string).GetMethod("Contains", new Type[] { typeof(string) }), new Expr[] { args[1] });
										break;
									case "startswith":
										if (args.Length == 2 && args[0].Type == typeof(string) && args[1].Type == typeof(string))
											return Expr.Call(args[0], typeof(string).GetMethod("StartsWith", new Type[] { typeof(string) }), new Expr[] { args[1] });
										break;
									case "endswith":
										if (args.Length == 2 && args[0].Type == typeof(string) && args[1].Type == typeof(string))
											return Expr.Call(args[0], typeof(string).GetMethod("EndsWith", new Type[] { typeof(string) }), new Expr[] { args[1] });
										break;
									case "indexof":
										if (args.Length == 2 && args[0].Type == typeof(string) && args[1].Type == typeof(string))
											return Expr.Call(args[0], typeof(string).GetMethod("IndexOf", new Type[] { typeof(string) }), new Expr[] { args[1] });
										break;
									case "length":
										if (args.Length == 1 && args[0].Type == typeof(string))
											return Expr.PropertyOrField(args[0], "Length");
										break;
								}
							}

							// Call ModelInstance.ToString(format) for model types.
							if (type.ModelType != null)
							{
								if (instance != null && id == "ToString" && args.Length == 1)
								{
									// ModelInstanceFormatter.ToString((IModelInstance)instance, format)
									return Expr.Call(
										// ModelInstanceFormatter.ToString
										typeof(ModelInstanceFormatter).GetMethod("ToString", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(IModelInstance), typeof(string) }, null),
										// (IModelInstance)instance, format
										new[]
										{
											// (IModelInstance)instance
											Expr.Convert(instance, typeof(IModelInstance)),
											// (format)
											args[0]
										});
								}
							}

							throw ParseError(errorPos, ParseErrorType.NoApplicableMethod, id, GetTypeName(type));
						case 1:
							MethodInfo method = (MethodInfo)mb;
							if (!IsPredefinedType(method.DeclaringType))
								throw ParseError(errorPos, ParseErrorType.MethodsAreInaccessible, GetTypeName(new ModelExpressionType(method.DeclaringType)));
							if (method.ReturnType == typeof(void))
								throw ParseError(errorPos, ParseErrorType.MethodIsVoid,
									id, GetTypeName(new ModelExpressionType(method.DeclaringType)));

							// Handle boxing of value types
							var parameters = method.GetParameters();
							for (var i = 0; i < args.Length; i++)
								if (args[i].Type.IsValueType && !parameters[i].ParameterType.IsValueType)
									args[i] = Expr.Convert(args[i], parameters[i].ParameterType);

							return Expr.Call(instance, (MethodInfo)method, args);
						default:
							throw ParseError(errorPos, ParseErrorType.AmbiguousMethodInvocation,
								id, GetTypeName(type));
					}
				}
				// Parse property
				else
				{
					if (type.ModelType != null)
					{
						ModelProperty property = type.ModelType.Properties[id];

						if (property != null)
						{
							var result = new ModelMemberExpression(instance, property);

							// Rename support
							if (RenameExpression != null)
								RenameExpression(result, token.pos);

							return result;
						}
						else
						{
							throw ParseError(errorPos, ParseErrorType.UnknownPropertyOrField,
								id, GetTypeName(type));
						}
					}
					else
					{
						MemberInfo member = FindPropertyOrField(type.Type, id, instance == null);
						if (member == null)
						{
							MethodBase mb;
							var args = new Expression[0];
							if (FindMethod(type.Type, id, instance == null, ref args, out mb) == 1)
							{
								MethodInfo method = (MethodInfo)mb;
								if (method.ReturnType == typeof(void))
									throw ParseError(errorPos, ParseErrorType.MethodIsVoid, id, GetTypeName(new ModelExpressionType(method.DeclaringType)));
								return Expr.Call(instance, method, new Expression[0]);
							}

							throw ParseError(errorPos, ParseErrorType.UnknownPropertyOrField,
								id, GetTypeName(type));
						}

						if (member is PropertyInfo)
							return Expr.Property(instance, (PropertyInfo)member);
						else
							return Expr.Field(instance, (FieldInfo)member);
					}
				}
			}

			static Type FindGenericType(Type generic, Type type)
			{
				while (type != null && type != typeof(object))
				{
					if (type.IsGenericType && type.GetGenericTypeDefinition() == generic) return type;
					if (generic.IsInterface)
					{
						foreach (Type intfType in type.GetInterfaces())
						{
							Type found = FindGenericType(generic, intfType);
							if (found != null) return found;
						}
					}
					type = type.BaseType;
				}
				return null;
			}

			Expression ParseAggregate(Expression instance, IExpressionType elementType, string methodName, int errorPos)
			{
				ModelParameterExpression outerIt = it;
				ModelParameterExpression innerIt = new ModelParameterExpression(elementType, "");
				it = innerIt;
				Expression[] args = ParseArgumentList();
				it = outerIt;

				MethodBase signature;
				if (FindMethod(typeof(IEnumerableSignatures), methodName, false, ref args, out signature) != 1)
					throw ParseError(errorPos, ParseErrorType.NoApplicableAggregate, methodName);

				Type[] typeArgs;

				// Contains
				//		bool Method<TSource>(IEnumerable<TSource> source, TSource value)
				if (signature.Name == "Contains")
					return Expr.Call(typeof(Enumerable), "Contains", new Type[] { elementType.Type }, new Expression[] { instance, args[0] });

				// Except
				//		IEnumerable<TSource> Except(IEnumerable<TSource> first, IEnumerable<TSource> second)
				if (signature.Name == "Except")
					return Expr.Call(typeof(Enumerable), "Except", new Type[] { elementType.Type }, new Expression[] { instance, args[0] });

				if (signature.Name == "Min" || signature.Name == "Max" || signature.Name == "Select" || signature.Name == "OrderBy" || signature.Name == "OrderByDescending")
				{
					// Methods with a second type parameter that determines the result type of a selector or comparer function.
					// Min, Max
					//		TResult Method<TSource, TResult>(IEnumerable<TItem> source, Func<TSource, TResult> selector)
					// Select
					//		IEnumerable<TResult> Select<TSource, TResult>(IEnumerable<TItem> source, Func<TSource, TResult> selector)
					// OrderBy, OrderByDescending
					//		IEnumerable<TSource> Method<TSource, TKey>(IEnumerable<TItem> source, Func<TSource, TKey> comparer)
					typeArgs = new Type[] { elementType.Type, args[0].Type };
				}
				else
				{
					// Methods that have only a single type parameter: no arguments, or optional predicate or item value.
					// First, FirstOrDefault, Last, LastOrDefault
					//		TSource Method<TSource>(IEnumerable<TSource> source)
					//		TSource Method<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate)
					// Where
					//		IEnumerable<TSource> Method<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate)
					// Any, All
					//		bool Method<TSource>(IEnumerable<TSource> source)
					//		bool Method<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate)
					// Count
					//		int Method<TSource>(IEnumerable<TSource> source)
					//		int Method<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate)
					// Except
					//		IEnumerable<TSource> Method<TSource>(IEnumerable<TSource> first, IEnumerable<TSource> second)
					typeArgs = new Type[] { elementType.Type };
				}

				if (args.Length == 0)
				{
					// Methods with no arguments that return a single value, e.g. `items.First()`, `items.Count()`, etc...
					args = new Expression[] { instance };
				}
				else
				{
					// Methods with a single lambda expression argument, e.g. `items.Where(selector)`, `items.Max(selector)`, etc...
					args = new Expression[] { instance, new ModelLambdaExpression(args[0], innerIt) };
				}

				// Some extension methods may need to be wrapped in some way, e.g. to track the model type of the result.
				switch (signature.Name)
				{
					// IEnumerable<TSource> -> TSource
					case "First":
					case "FirstOrDefault":
					case "Last":
					case "LastOrDefault":

						return new ModelCastExpression(Expr.Call(typeof(Enumerable), signature.Name, typeArgs, args), elementType.ModelType, false);

					// IEnumerable<TSource> -> IEnumerable<TSource>
					case "Where":
					case "OrderBy":
					case "OrderByDescending":

						return new ModelCastExpression(Expr.Call(typeof(Enumerable), signature.Name, typeArgs, args), elementType.ModelType, true);

					// IEnumerable<TSource> -> TResult
					case "Select":

						var resultType = typeArgs[1];

						if (typeof(IModelInstance).IsAssignableFrom(resultType))
						{
							var selector = args[1];
							var modelType = GetModelType(selector);
							if (modelType == null)
								modelType = ModelContext.Current.GetModelType(resultType);

							return new ModelCastExpression(Expr.Call(typeof(Enumerable), signature.Name, typeArgs, args), modelType, true);
						}

						break;

					case "Average":
					case "Min":
					case "Max":
					case "Sum":
						Expression left = instance;
						var lambdaBody = ((ModelLambdaExpression)args[1]).Body;

						if (lambdaBody is MemberExpression)
							lambdaBody = ((MemberExpression)lambdaBody).Expression;

						if (!IsNullableType(lambdaBody.Type))
							break;

						var nullsFiltered = Expr.Call(typeof(Enumerable), "Where",
							new Type[] { typeArgs[0] },
							new Expression[] { args[0], new ModelLambdaExpression(Expr.NotEqual(lambdaBody, Expr.Constant(null, lambdaBody.Type)), innerIt) });

						args[0] = new ModelCastExpression(nullsFiltered, elementType.ModelType, true);

						break;
				}

				return Expr.Call(typeof(Enumerable), signature.Name, typeArgs, args);
			}

			private static ModelType GetModelType(Expression expr)
			{
				var propExpr = expr as ModelMemberExpression;
				if (propExpr != null)
				{
					// Simple property expression
					return propExpr.ModelType;
				}

				var call = expr as MethodCallExpression;
				if (call != null && call.Method.DeclaringType != null && call.Method.DeclaringType.FullName == "System.Linq.Enumerable")
				{
					var source = call.Arguments[0];

					switch (call.Method.Name)
					{
						case "First":
						case "Last":
						case "Where":

							// TSource First<TSource>(IEnumerable<TSource> source)
							// TSource First<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate)
							// TSource Last<TSource>(IEnumerable<TSource> source)
							// TSource Last<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate)
							// IEnumerable<TSource> Where<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate)

							return GetModelType(source);

						case "Select":

							if (call.Arguments.Count == 2)
							{
								// TResult Select<TSource, TResult>(IEnumerable<TSource> source, Func<TSource, TResult> selector)

								var selectorLambda = call.Arguments[1] as ModelLambdaExpression;
								if (selectorLambda != null)
									return GetModelType(selectorLambda.Body);
							}

							break;

					}
				}

				return null;
			}

			Expression[] ParseArgumentList()
			{
				ValidateToken(TokenId.OpenParen, ParseErrorType.OpenParenExpected);
				NextToken();
				Expression[] args = token.id != TokenId.CloseParen ? ParseArguments() : new Expression[0];
				ValidateToken(TokenId.CloseParen, ParseErrorType.CloseParenOrCommaExpected);
				NextToken();
				return args;
			}

			Expression[] ParseArguments()
			{
				List<Expression> argList = new List<Expression>();
				while (true)
				{
					argList.Add(ParseExpression());
					if (token.id != TokenId.Comma) break;
					NextToken();
				}
				return argList.ToArray();
			}

			Expression ParseArrayLiteral()
			{
				int errorPos = token.pos;
				ValidateToken(TokenId.OpenBracket, ParseErrorType.OpenParenExpected);
				NextToken();
				Expression[] elements = ParseArguments();
				ValidateToken(TokenId.CloseBracket, ParseErrorType.CloseBracketOrCommaExpected);
				NextToken();
				if (elements.Length == 0)
					return Expr.Constant(new object[0]);

				// Infer the type of the array based on the types of the array elements
				Type arrayType = null;
				bool includesNulls = false;
				foreach (var element in elements)
				{
					// Track the fact that a null element was encountered, meaning that the array cannot be a value type
					if (element == nullLiteral)
						includesNulls = true;

					// Initialize the array type based on the type of the first non-null element
					if (arrayType == null)
					{
						if (element != nullLiteral)
							arrayType = element.Type;
					}

					// Otherwise, ensure subsequent elements are compatible with the array type, or downcast accordingly
					else
					{
						if (element.Type == arrayType || IsCompatibleWith(element.Type, arrayType))
							continue;
						if (IsCompatibleWith(arrayType, element.Type))
							arrayType = element.Type;
						else
							arrayType = typeof(object);
					}
				}

				// Force the type to be object if no type was determined or the array contained a null value amongst compatible value types
				if (arrayType == null || (includesNulls && arrayType.IsValueType))
					arrayType = typeof(object);

				// Handling boxing of value types and casting of null literals
				if (!arrayType.IsValueType)
				{
					for (var i = 0; i < elements.Length; i++)
						if (elements[i].Type.IsValueType || elements[i] == nullLiteral)
							elements[i] = Expr.Convert(elements[i], arrayType);
				}

				// Handling casting of literals
				else
				{
					for (var i = 0; i < elements.Length; i++)
						if (elements[i].Type != arrayType)
							elements[i] = Expr.Convert(elements[i], arrayType);
				}

				return Expr.NewArrayInit(arrayType, elements);
			}

			Expression ParseElementAccess(Expression expr)
			{
				int errorPos = token.pos;
				ValidateToken(TokenId.OpenBracket, ParseErrorType.OpenParenExpected);
				NextToken();
				Expression[] args = ParseArguments();
				ValidateToken(TokenId.CloseBracket, ParseErrorType.CloseBracketOrCommaExpected);
				NextToken();
				if (expr.Type.IsArray)
				{
					if (expr.Type.GetArrayRank() != 1 || args.Length != 1)
						throw ParseError(errorPos, ParseErrorType.CannotIndexMultiDimArray);
					Expression index = PromoteExpression(args[0], typeof(int), true);
					if (index == null)
						throw ParseError(errorPos, ParseErrorType.InvalidIndex);
					return Expr.ArrayIndex(expr, index);
				}
				else
				{
					MethodBase mb;
					switch (FindIndexer(expr.Type, ref args, out mb))
					{
						case 0:
							throw ParseError(errorPos, ParseErrorType.NoApplicableIndexer,
								GetTypeName(expr));
						case 1:
							return Expr.Call(expr, (MethodInfo)mb, args);
						default:
							throw ParseError(errorPos, ParseErrorType.AmbiguousIndexerInvocation,
								GetTypeName(expr));
					}
				}
			}

			static bool IsPredefinedType(Type type)
			{
				foreach (Type t in predefinedTypes) if (t == type) return true;
				return false;
			}

			static bool IsNullableType(Type type)
			{
				return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
			}

			static Type GetNonNullableType(Type type)
			{
				return IsNullableType(type) ? type.GetGenericArguments()[0] : type;
			}

			static Type GetNullableType(Type type)
			{
				if (IsNullableType(type))
					return type;
				if (type.IsValueType)
					return typeof(Nullable<>).MakeGenericType(type);
				else
					return type;
			}

			static string GetTypeName(Expression expr)
			{
				if (expr is IExpressionType)
				{
					return GetTypeName(((IExpressionType)expr));
				}

				Type baseType = GetNonNullableType(expr.Type);
				string s = baseType.Name;
				if (expr.Type != baseType) s += '?';
				return s;
			}

			static string GetTypeName(IExpressionType type)
			{
				if (type.ModelType != null)
				{
					return type.ModelType.Name;
				}

				Type baseType = GetNonNullableType(type.Type);
				string s = baseType.Name;
				if (type.Type != baseType) s += '?';
				return s;
			}

			static bool IsNumericType(Type type)
			{
				return GetNumericTypeKind(type) != 0;
			}

			static bool IsSignedIntegralType(Type type)
			{
				return GetNumericTypeKind(type) == 2;
			}

			static bool IsUnsignedIntegralType(Type type)
			{
				return GetNumericTypeKind(type) == 3;
			}

			static int GetNumericTypeKind(Type type)
			{
				type = GetNonNullableType(type);
				if (type.IsEnum) return 0;
				switch (Type.GetTypeCode(type))
				{
					case TypeCode.Char:
					case TypeCode.Single:
					case TypeCode.Double:
					case TypeCode.Decimal:
						return 1;
					case TypeCode.SByte:
					case TypeCode.Int16:
					case TypeCode.Int32:
					case TypeCode.Int64:
						return 2;
					case TypeCode.Byte:
					case TypeCode.UInt16:
					case TypeCode.UInt32:
					case TypeCode.UInt64:
						return 3;
					default:
						return 0;
				}
			}

			static bool IsEnumType(Type type)
			{
				return GetNonNullableType(type).IsEnum;
			}

			void CheckAndPromoteOperand(Type signatures, string opName, ref Expression expr, int errorPos)
			{
				expr = CheckAndPromoteNullable(expr, signatures, errorPos);
				Expression[] args = new Expression[] { expr };
				MethodBase method;
				if (FindMethod(signatures, "F", false, ref args, out method) != 1)
					throw ParseError(errorPos, ParseErrorType.IncompatibleOperand,
						opName, GetTypeName(args[0]));
				expr = args[0];
			}

			void CheckAndPromoteOperands(Type signatures, string opName, ref Expression left, ref Expression right, int errorPos)
			{
				left = CheckAndPromoteNullable(left, signatures, errorPos);
				right = CheckAndPromoteNullable(right, signatures, errorPos);
				Expression[] args = new Expression[] { left, right };
				args = CheckAndPromoteDateTimeComparison(args, signatures, errorPos);
				MethodBase method;
				if (FindMethod(signatures, "F", false, ref args, out method, signatures != typeof(IEqualitySignatures)) != 1)
					throw IncompatibleOperandsError(opName, left, right, errorPos);
				left = args[0];
				right = args[1];
			}

			// For binary and unary expressions, promote nullable types to their underlying type's default value
			Expression CheckAndPromoteNullable(Expression expr, Type signatures, int errorPos)
			{
				// Ignore types that cannot have a value of null
				if (!IsNullableType(expr.Type) && expr.Type.IsValueType)
					return expr;

				Type type = GetNonNullableType(expr.Type);

				// If performing arithmetic on numbers, set null values to 0 (default value)
				if (IsNumericType(type) && signatures != typeof(IEqualitySignatures))
				{
					if (expr is ConstantExpression)
						return ((ConstantExpression)expr).Value == null ? Expr.Constant(Activator.CreateInstance(type)) : expr;
					else
						return Expr.Coalesce(expr, GenerateConversion(Expr.Constant(Activator.CreateInstance(type)), expr.Type, errorPos));
				}

				// Set null values to 1/1/1970 (default value)
				else if (type == typeof(DateTime) && signatures != typeof(IEqualitySignatures))
				{
					if (expr is ConstantExpression)
						return ((ConstantExpression)expr).Value == null ? Expr.Constant(Activator.CreateInstance(type)) : expr;
					else
						return Expr.Coalesce(expr, GenerateConversion(Expr.Constant(Activator.CreateInstance(type)), expr.Type, errorPos));
				}

				// For relational/comparison expressions set null values to empty string
				else if (type == typeof(String) && signatures == (typeof(IRelationalSignatures)))
				{
					if (expr is ConstantExpression)
						return ((ConstantExpression)expr).Value == null ? Expr.Constant("") : expr;
					else
						return Expr.Coalesce(expr, Expr.Constant(""));
				}

				return expr;
			}

			// For certain binary expressions containing a String and DateTime, try to promote String to DateTime
			Expression[] CheckAndPromoteDateTimeComparison(Expression[] args, Type signatures, int errorPos)
			{
				// Return if not a supported type
				if (signatures != typeof(ISubtractSignatures) && signatures != typeof(IEqualitySignatures) && signatures != typeof(IRelationalSignatures))
					return args;

				// Check if one side is DateTime and the other is String
				if (GetNonNullableType(args[0].Type) == typeof(DateTime) && args[1].Type == typeof(String))
					args[1] = PromoteExpression(args[1], typeof(DateTime), true) ?? args[1];
				else if (args[0].Type == typeof(String) && GetNonNullableType(args[1].Type) == typeof(DateTime))
					args[0] = PromoteExpression(args[0], typeof(DateTime), true) ?? args[0];

				return args;
			}

			Exception IncompatibleOperandsError(string opName, Expression left, Expression right, int pos)
			{
				return ParseError(pos, ParseErrorType.IncompatibleOperands,
					opName, GetTypeName(left), GetTypeName(right));
			}

			MemberInfo FindPropertyOrField(Type type, string memberName, bool staticAccess)
			{
				BindingFlags flags = BindingFlags.Public | BindingFlags.DeclaredOnly |
					(staticAccess ? BindingFlags.Static : BindingFlags.Instance);
				foreach (Type t in SelfAndBaseTypes(type))
				{
					MemberInfo[] members = t.FindMembers(MemberTypes.Property | MemberTypes.Field,
						flags, Type.FilterNameIgnoreCase, memberName);
					if (members.Length != 0) return members[0];
				}
				return null;
			}

			int FindMethod(Type type, string methodName, bool staticAccess, ref Expression[] args, out MethodBase method, bool coerceNullable = true)
			{
				for (int i = 0; i < args.Length; i++)
				{
					// Coerce nullable expressions to their non-nullable equivalents by accessing the Value property.
					if (IsNullableType(args[i].Type) && coerceNullable)
						args[i] = Expr.MakeMemberAccess(args[i], args[i].Type.GetMember("Value")[0]);
				}
				BindingFlags flags = BindingFlags.Public | BindingFlags.DeclaredOnly |
					(staticAccess ? BindingFlags.Static : BindingFlags.Instance);
				foreach (Type t in SelfAndBaseTypes(type))
				{
					MemberInfo[] members = t.FindMembers(MemberTypes.Method,
						flags, Type.FilterNameIgnoreCase, methodName);
					int count = FindBestMethod(members.Cast<MethodBase>(), ref args, out method);
					if (count != 0) return count;
				}
				method = null;
				return 0;
			}

			int FindIndexer(Type type, ref Expression[] args, out MethodBase method)
			{
				foreach (Type t in SelfAndBaseTypes(type))
				{
					MemberInfo[] members = t.GetDefaultMembers();
					if (members.Length != 0)
					{
						IEnumerable<MethodBase> methods = members.
							OfType<PropertyInfo>().
							Select(p => (MethodBase)p.GetGetMethod()).
							Where(m => m != null);
						int count = FindBestMethod(methods, ref args, out method);
						if (count != 0) return count;
					}
				}
				method = null;
				return 0;
			}

			static IEnumerable<Type> SelfAndBaseTypes(Type type)
			{
				if (type.IsInterface)
				{
					List<Type> types = new List<Type>();
					AddInterface(types, type);
					return types;
				}
				return SelfAndBaseClasses(type);
			}

			static IEnumerable<Type> SelfAndBaseClasses(Type type)
			{
				while (type != null)
				{
					yield return type;
					type = type.BaseType;
				}
			}

			static void AddInterface(List<Type> types, Type type)
			{
				if (!types.Contains(type))
				{
					foreach (Type t in type.GetInterfaces()) AddInterface(types, t);
					types.Add(type);
				}
			}

			class MethodData
			{
				public MethodBase MethodBase;
				public ParameterInfo[] Parameters;
				public Expression[] Args;
			}

			/// <summary>
			/// Finds the best matching method from a set of qualifying methods based on the quantity and types of the arguments.
			/// </summary>
			/// <param name="methods"></param>
			/// <param name="args"></param>
			/// <param name="method"></param>
			/// <returns></returns>
			int FindBestMethod(IEnumerable<MethodBase> methods, ref Expression[] args, out MethodBase method)
			{
				var searchArgs = args;
				MethodData[] applicable = methods.
					Select(m => new MethodData { MethodBase = m, Parameters = m.GetParameters() }).
					Where(m => IsApplicable(m, searchArgs)).
					ToArray();
				if (applicable.Length > 1)
				{
					applicable = applicable.
						Where(m => applicable.All(n => m == n || IsBetterThan(searchArgs, m, n))).
						ToArray();
				}
				// Check for param arrays
				if (applicable.Length == 0)
				{
					applicable = methods.
						Where(m =>
							m.GetParameters().Length > 0 && // At least one parameter exists
							m.GetParameters().Last().GetCustomAttributes(typeof(ParamArrayAttribute), false).Length > 0 && // The last parameter is a param array
							searchArgs.Length >= m.GetParameters().Length - 1). // The set of arguments passed could be enough
						OrderBy(m => -m.GetParameters().Length). // Sort from most number of arguments to least
						Select(m =>
						{
							// See if the additional arguments can be promoted into an array of the correct type
							var parameters = m.GetParameters();
							var arrayType = parameters.Last().ParameterType.GetElementType();
							var elements = searchArgs.Skip(parameters.Length - 1).Select(a => PromoteExpression(a, arrayType, false)).ToArray();
							if (elements.Any(e => e == null))
								return null;

							// If only one parameter was specified for a params array and it is enumerable, attempt to coerce into an array
							if (elements.Length == 1 && elements[0].Type.IsGenericType && elements[0].Type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
							{
								var enumerableType = elements[0].Type.GetGenericArguments()[0];

								// Handling boxing of value types and casting of null literals
								if (!arrayType.IsValueType)
								{
									if (enumerableType.IsValueType || elements[0] == nullLiteral)
									{
										elements = new Expression[] { Expr.Call(typeof(Enumerable).GetMethod("Cast").MakeGenericMethod(arrayType), elements[0]) };
										enumerableType = arrayType;
									}
								}

								elements = new Expression[] { Expr.Call(typeof(Enumerable).GetMethod("ToArray").MakeGenericMethod(enumerableType), elements[0]) };
							}

							// Otherwise, coerce into an array literal
							else
							{
								// Handling boxing of value types and casting of null literals
								if (!arrayType.IsValueType)
								{
									for (var i = 0; i < elements.Length; i++)
										if (elements[i].Type.IsValueType || elements[i] == nullLiteral)
											elements[i] = Expr.Convert(elements[i], arrayType);
								}

								elements = new Expression[] { Expr.NewArrayInit(arrayType, elements) };
							}

							// Return the matching method information
							return new MethodData()
							{
								MethodBase = m,
								Parameters = parameters,
								Args = searchArgs.Take(parameters.Length - 1).Concat(elements).ToArray()
							};
						}).
						Where(m => m != null).
						Take(1).
						ToArray();
				}
				if (applicable.Length == 1)
				{
					MethodData md = applicable[0];
					args = md.Args;
					method = md.MethodBase;
				}
				else
				{
					method = null;
				}
				return applicable.Length;
			}

			bool IsApplicable(MethodData method, Expression[] args)
			{
				if (method.Parameters.Length != args.Length) return false;
				Expression[] promotedArgs = new Expression[args.Length];
				for (int i = 0; i < args.Length; i++)
				{
					ParameterInfo pi = method.Parameters[i];
					if (pi.IsOut) return false;
					Expression promoted = PromoteExpression(args[i], pi.ParameterType, false);
					if (promoted == null) return false;
					promotedArgs[i] = promoted;
				}
				method.Args = promotedArgs;
				return true;
			}

			Expression PromoteExpression(Expression expr, Type type, bool exact)
			{
				if (expr.Type == type) return expr;
				if (expr is ConstantExpression)
				{
					ConstantExpression ce = (ConstantExpression)expr;
					if (ce == nullLiteral)
					{
						if (!type.IsValueType || IsNullableType(type))
							return Expr.Constant(null, type);
					}
					else
					{
						string text;
						if (literals.TryGetValue(ce, out text))
						{
							Type target = GetNonNullableType(type);
							Object value = null;
							switch (Type.GetTypeCode(ce.Type))
							{
								case TypeCode.Int32:
								case TypeCode.UInt32:
								case TypeCode.Int64:
								case TypeCode.UInt64:
									value = ParseNumber(text, target);
									break;
								case TypeCode.Double:
									if (target == typeof(decimal)) value = ParseNumber(text, target);
									break;
								case TypeCode.String:

									if (type.IsEnum)
										value = ParseEnum(text, target);

									else if (type == typeof(DateTime))
									{
										var result = ParseDateTime(text);
										if (result != null)
											return result;
									}

									else if (type == typeof(DateTime?))
									{
										if (String.IsNullOrEmpty(text))
											return Expr.Constant(null, typeof(DateTime?));
										else
										{
											var result = ParseDateTime(text);
											if (result != null)
												return result;
										}

									}
									break;
							}
							if (value != null && type.IsAssignableFrom(value.GetType()))
								return Expr.Constant(value, type);
						}
					}
				}
				else if (type == typeof(string))
				{
					bool isModelType;
					bool isList;
					Type itemType;
					string format;
					IFormatProvider provider;

					if (expr is ModelCastExpression)
					{
						isModelType = true;
						isList = ((ModelCastExpression)expr).IsList;
						itemType = typeof(IModelInstance);
						if (!ExpressionFormatter.TryGetFormat(((ModelCastExpression)expr).Expression, CultureInfo.CurrentCulture, out format, out provider))
						{
							// Fall back to the type-level format for model cast expressions
							if (((ModelCastExpression)expr).ModelType != null)
								format = ((ModelCastExpression)expr).ModelType.Format;
						}
					}
					else
					{
						ExpressionHelper.GetTypeInfo(expr, out isModelType, out isList, out itemType);
						ExpressionFormatter.TryGetFormat(expr, CultureInfo.CurrentCulture, out format, out provider);
					}

					if (isList)
					{
						// Use String.Join(", ", enumerableList) to print out an IEnumerable list
						if (!string.IsNullOrEmpty(format))
						{
							if (isModelType)
							{
								// Use IModelInstance.ToString(format, provider) ...

								// IEnumerable<IModelInstance> Cast<IModelInstance>(this IEnumerable expr)
								expr = ExpressionWriter.CallLinqCast<IModelInstance>(expr);

								// IEnumerable<string> Select<IModelInstance, string>(IEnumerable<IModelInstance> list, (IModelInstance i) => ModelInstanceFormatter.ToString(i, format))
								expr = ExpressionWriter.CallLinqSelect<IModelInstance, string>(expr, i =>
									// ModelInstanceFormatter.ToString(i, format)
									Expr.Call(
										// ModelInstanceFormatter.ToString
										typeof(ModelInstanceFormatter).GetMethod("ToString", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(IModelInstance), typeof(string) }, null),
										// (i, format)
										new[]
										{
											// i
											i,
											// format
											Expr.Constant(format)
										})
									);
							}
							else
							{
								// IEnumerable<object> Cast<object>(this IEnumerable expr)
								expr = ExpressionWriter.CallLinqCast<object>(expr);

								// Select<TItem, string>(expr, (TItem i) => i.ToString(format, provider))
								expr = ExpressionWriter.CallLinqSelect<string>(expr, itemType, i =>
								{
									Expression toStringExpr;

									// i.ToString(format, provider)
									if (!ExpressionWriter.TryCallToString(i, format, provider, out toStringExpr))
										// i.ToString()
										toStringExpr = ExpressionWriter.CallToString(i);

									return toStringExpr;
								});
							}

							// string.Join(", ", string[])
							expr = ExpressionWriter.CallStringJoin(", ", ExpressionWriter.CallLinqToArray(expr, typeof(string)), typeof(string[]));
						}
						else if (itemType == typeof(string))
						{
							// string.Join(", ", string[])
							expr = ExpressionWriter.CallStringJoin(", ", ExpressionWriter.CallLinqToArray(expr, typeof(string)), typeof(string[]));
						}
						else
						{
							// IEnumerable<object> Cast<object>(this IEnumerable expr)
							expr = ExpressionWriter.CallLinqCast<object>(expr);

							// string.Join(", ", object[])
							expr = ExpressionWriter.CallStringJoin(", ", ExpressionWriter.CallLinqToArray(expr, typeof(object)), typeof(object[]));
						}
					}
					else if (isModelType)
					{
						if (!String.IsNullOrEmpty(format))
							expr = ExpressionWriter.CallModelInstanceToString(expr, format);
						else
							expr = ExpressionWriter.CallModelInstanceToString(expr);
					}
					else if (IsNullableType(expr.Type))
					{
						Expression toStringExpr;

						if (format == null && expr.Type == typeof(DateTime?))
							format = "g";

						// expr.ToString(format, provider)
						if (format == null || !ExpressionWriter.TryCallToString(Expr.Convert(expr, Nullable.GetUnderlyingType(expr.Type)), format, provider, out toStringExpr))
							// expr.ToString()
							toStringExpr = ExpressionWriter.CallToString(expr);

						// expr == null ? "" : ((T)expr).ToString(format, provider)
						expr = Expr.Condition(
							Expr.Equal(expr, Expr.Constant(null)),
							Expr.Constant(""),
							toStringExpr);
					}
					else
					{
						Expression toStringExpr;

						if (format == null && expr.Type == typeof(DateTime))
							format = "g";

						// expr.ToString(format, provider)
						if (format == null || !ExpressionWriter.TryCallToString(expr, format, provider, out toStringExpr))
							// expr.ToString()
							toStringExpr = ExpressionWriter.CallToString(expr);

						expr = toStringExpr;
					}

					return expr;
				}

				if (IsCompatibleWith(expr.Type, type))
				{
					if (type.IsValueType || exact) return Expr.Convert(expr, type);
					return expr;
				}

				return null;
			}

			static object ParseNumber(string text, Type type)
			{
				switch (Type.GetTypeCode(GetNonNullableType(type)))
				{
					case TypeCode.SByte:
						sbyte sb;
						if (sbyte.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out sb)) return sb;
						break;
					case TypeCode.Byte:
						byte b;
						if (byte.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out b)) return b;
						break;
					case TypeCode.Int16:
						short s;
						if (short.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out s)) return s;
						break;
					case TypeCode.UInt16:
						ushort us;
						if (ushort.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out us)) return us;
						break;
					case TypeCode.Int32:
						int i;
						if (int.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out i)) return i;
						break;
					case TypeCode.UInt32:
						uint ui;
						if (uint.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out ui)) return ui;
						break;
					case TypeCode.Int64:
						long l;
						if (long.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out l)) return l;
						break;
					case TypeCode.UInt64:
						ulong ul;
						if (ulong.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out ul)) return ul;
						break;
					case TypeCode.Single:
						float f;
						if (float.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out f)) return f;
						break;
					case TypeCode.Double:
						double d;
						if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out d)) return d;
						break;
					case TypeCode.Decimal:
						decimal e;
						if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out e)) return e;
						break;
				}
				return null;
			}

			static object ParseEnum(string name, Type type)
			{
				if (type.IsEnum)
				{
					MemberInfo[] memberInfos = type.FindMembers(MemberTypes.Field,
						BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Static,
						Type.FilterNameIgnoreCase, name);
					if (memberInfos.Length != 0)
						return ((FieldInfo)memberInfos[0]).GetValue(null);
					else
					{
						foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Static))
						{
							if (name.Replace(" ", "").Equals(field.Name, StringComparison.InvariantCultureIgnoreCase))
								return field.GetValue(null);
							var description = field.GetCustomAttributes(typeof(DescriptionAttribute), false).FirstOrDefault() as DescriptionAttribute;
							if (description != null && name.Replace(" ", "").Equals(description.Description.Replace(" ", ""), StringComparison.InvariantCultureIgnoreCase))
								return field.GetValue(null);
						}
					}
				}
				return null;
			}

			const string dateTimeUnits = "year|month|week|day|hour|minute|second";

			/// <summary>
			/// Ex: "last year"
			/// </summary>
			static readonly Regex basicRelativeRegex = new Regex(@"^(last|next) +(" + dateTimeUnits + ")$");

			/// <summary>
			/// Ex: "+1 week"
			/// Ex: " 1week"
			/// </summary>
			static readonly Regex simpleRelativeRegex = new Regex(@"^([+-]?\d+) *(" + dateTimeUnits + ")s?$");

			/// <summary>
			/// Ex: "2 minutes"
			/// Ex: "3 months 5 days 1 hour ago"
			/// </summary>
			static readonly Regex completeRelativeRegex = new Regex(@"^(?: *(\d) *(" + dateTimeUnits + ")s?)+( +ago| +from now)?$");

			/// <summary>
			/// Parse a date/time string.
			/// 
			/// Can handle relative English-written date times like:
			///  - "-1 day": Yesterday
			///  - "+12 weeks": Today twelve weeks later
			///  - "1 seconds": One second later from now.
			///  - "5 days 1 hour ago"
			///  - "1 year 2 months 3 weeks 4 days 5 hours 6 minutes 7 seconds"
			///  - "today": This day at midnight.
			///  - "now": Right now (date and time).
			///  - "next week"
			///  - "last month"
			///  - "2010-12-31"
			///  - "01/01/2010 1:59 PM"
			///  - "23:59:58": Today at the given time.
			/// 
			/// If the relative time includes hours, minutes or seconds, it's relative to now,
			/// else it's relative to today.
			/// </summary>
			static Expression ParseDateTime(string input)
			{
				// Return null if the string is blank
				if (String.IsNullOrWhiteSpace(input))
					return null;

				// Try parse fixed dates like "01/01/2000".
				DateTime dateLiteral;
				if (DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal | DateTimeStyles.AllowInnerWhite | DateTimeStyles.AllowLeadingWhite | DateTimeStyles.AllowTrailingWhite | DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.NoCurrentDateDefault, out dateLiteral))
				{
					// Assume this is a time if the year, month and day are all 1 (NoCurrentDefaultDate kicked in)
					if (dateLiteral.Year == 1 && dateLiteral.Month == 1 && dateLiteral.Day == 1)
						dateLiteral = dateLiteral.AddYears(1969);

					return Expr.Constant(dateLiteral);
				}

				// Prepare for parsing
				input = input.Trim().ToLower();

				// Now, Today, Tomorrow, Yesterday
				switch (input)
				{
					case "now":
						return Expr.Property(null, typeof(DateTime), "Now");
					case "today":
						return Expr.Property(null, typeof(DateTime), "Today");
					case "tomorrow":
						return Expr.Call(Expr.Property(null, typeof(DateTime), "Today"), typeof(DateTime).GetMethod("AddDays"), Expr.Constant(1d));
					case "yesterday":
						return Expr.Call(Expr.Property(null, typeof(DateTime), "Today"), typeof(DateTime).GetMethod("AddDays"), Expr.Constant(-1d));
				}

				// Next/Last Year/Month/Week/Day/Hour/Minute/Second
				var match = basicRelativeRegex.Match(input);
				if (match.Success)
				{
					var unit = match.Groups[2].Value;
					var sign = string.Compare(match.Groups[1].Value, "next", true) == 0 ? 1 : -1;
					return AddOffset(unit, sign);
				}

				// Try simple format like "+1 week".
				match = simpleRelativeRegex.Match(input);
				if (match.Success)
				{
					var delta = Convert.ToDouble(match.Groups[1].Value);
					var unit = match.Groups[2].Value;
					return AddOffset(unit, delta);
				}

				// Try first the full format like "1 day 2 hours 10 minutes ago".
				match = completeRelativeRegex.Match(input);
				if (match.Success)
				{
					var values = match.Groups[1].Captures;
					var units = match.Groups[2].Captures;
					var sign = match.Groups[3].Success && match.Groups[3].Value.EndsWith("ago") ? -1 : 1;

					// Determine whether to start with Today or Now
					Expression result = Expr.Property(null, typeof(DateTime), "Today");
					foreach (Capture unit in units)
					{
						if (UnitIncludesTime(unit.Value))
						{
							result = Expr.Property(null, typeof(DateTime), "Now");
							break;
						}
					}

					for (int i = 0; i < values.Count; ++i)
					{
						var value = sign * Convert.ToInt32(values[i].Value);
						var unit = units[i].Value;

						result = AddOffset(unit, value, result);
					}

					return result;
				}

				return null;
			}

			/// <summary>
			/// Add/Remove years/days/hours... to a datetime expression.
			/// </summary>
			/// <param name="unit">Must be one of ValidUnits</param>
			/// <param name="offset">Value in given unit to add to the datetime</param>
			/// <param name="date">Relative datetime</param>
			/// <returns>Relative datetime</returns>
			static Expression AddOffset(string unit, double offset, Expression date)
			{
				switch (unit)
				{
					case "year":
						return Expr.Call(date, typeof(DateTime).GetMethod("AddYears"), Expr.Constant((int)offset));
					case "month":
						return Expr.Call(date, typeof(DateTime).GetMethod("AddMonths"), Expr.Constant((int)offset));
					case "week":
						return Expr.Call(date, typeof(DateTime).GetMethod("AddDays"), Expr.Constant(offset * 7));
					case "day":
						return Expr.Call(date, typeof(DateTime).GetMethod("AddDays"), Expr.Constant(offset));
					case "hour":
						return Expr.Call(date, typeof(DateTime).GetMethod("AddHours"), Expr.Constant(offset));
					case "minute":
						return Expr.Call(date, typeof(DateTime).GetMethod("AddMinutes"), Expr.Constant(offset));
					case "second":
						return Expr.Call(date, typeof(DateTime).GetMethod("AddSeconds"), Expr.Constant(offset));
				}

				return null;
			}

			/// <summary>
			/// Add/Remove years/days/hours... relative to today or now.
			/// </summary>
			/// <param name="unit">Must be one of ValidUnits</param>
			/// <param name="offset">Value in given unit to add to the datetime</param>
			/// <returns>Relative datetime</returns>
			static Expression AddOffset(string unit, double offset)
			{
				return AddOffset(unit, offset, Expr.Property(null, typeof(DateTime), UnitIncludesTime(unit) ? "Now" : "Today"));
			}

			static bool UnitIncludesTime(string unit)
			{
				switch (unit)
				{
					case "hour":
					case "minute":
					case "second":
						return true;

					default:
						return false;
				}
			}

			static bool IsCompatibleWith(Type source, Type target)
			{
				if (source == target) return true;
				if (!target.IsValueType) return target.IsAssignableFrom(source);
				Type st = GetNonNullableType(source);
				Type tt = GetNonNullableType(target);
				if (st != source && tt == target) return false;
				TypeCode sc = st.IsEnum ? TypeCode.Object : Type.GetTypeCode(st);
				TypeCode tc = tt.IsEnum ? TypeCode.Object : Type.GetTypeCode(tt);
				switch (sc)
				{
					case TypeCode.SByte:
						switch (tc)
						{
							case TypeCode.SByte:
							case TypeCode.Int16:
							case TypeCode.Int32:
							case TypeCode.Int64:
							case TypeCode.Single:
							case TypeCode.Double:
							case TypeCode.Decimal:
								return true;
						}
						break;
					case TypeCode.Byte:
						switch (tc)
						{
							case TypeCode.Byte:
							case TypeCode.Int16:
							case TypeCode.UInt16:
							case TypeCode.Int32:
							case TypeCode.UInt32:
							case TypeCode.Int64:
							case TypeCode.UInt64:
							case TypeCode.Single:
							case TypeCode.Double:
							case TypeCode.Decimal:
								return true;
						}
						break;
					case TypeCode.Int16:
						switch (tc)
						{
							case TypeCode.Int16:
							case TypeCode.Int32:
							case TypeCode.Int64:
							case TypeCode.Single:
							case TypeCode.Double:
							case TypeCode.Decimal:
								return true;
						}
						break;
					case TypeCode.UInt16:
						switch (tc)
						{
							case TypeCode.UInt16:
							case TypeCode.Int32:
							case TypeCode.UInt32:
							case TypeCode.Int64:
							case TypeCode.UInt64:
							case TypeCode.Single:
							case TypeCode.Double:
							case TypeCode.Decimal:
								return true;
						}
						break;
					case TypeCode.Int32:
						switch (tc)
						{
							case TypeCode.Int32:
							case TypeCode.Int64:
							case TypeCode.Single:
							case TypeCode.Double:
							case TypeCode.Decimal:
								return true;
						}
						break;
					case TypeCode.UInt32:
						switch (tc)
						{
							case TypeCode.UInt32:
							case TypeCode.Int64:
							case TypeCode.UInt64:
							case TypeCode.Single:
							case TypeCode.Double:
							case TypeCode.Decimal:
								return true;
						}
						break;
					case TypeCode.Int64:
						switch (tc)
						{
							case TypeCode.Int64:
							case TypeCode.Single:
							case TypeCode.Double:
							case TypeCode.Decimal:
								return true;
						}
						break;
					case TypeCode.UInt64:
						switch (tc)
						{
							case TypeCode.UInt64:
							case TypeCode.Single:
							case TypeCode.Double:
							case TypeCode.Decimal:
								return true;
						}
						break;
					case TypeCode.Single:
						switch (tc)
						{
							case TypeCode.Single:
							case TypeCode.Double:
								return true;
						}
						break;
					case TypeCode.Double:
						switch (tc)
						{
							case TypeCode.Decimal:
								return true;
						}
						break;
					default:
						if (st == tt) return true;
						break;
				}
				return false;
			}

			static bool IsBetterThan(Expression[] args, MethodData m1, MethodData m2)
			{
				bool better = false;
				for (int i = 0; i < args.Length; i++)
				{
					int c = CompareConversions(args[i].Type,
						m1.Parameters[i].ParameterType,
						m2.Parameters[i].ParameterType);
					if (c < 0) return false;
					if (c > 0) better = true;
				}
				return better;
			}

			// Return 1 if s -> t1 is a better conversion than s -> t2
			// Return -1 if s -> t2 is a better conversion than s -> t1
			// Return 0 if neither conversion is better
			static int CompareConversions(Type s, Type t1, Type t2)
			{
				if (t1 == t2) return 0;
				if (s == t1) return 1;
				if (s == t2) return -1;
				bool t1t2 = IsCompatibleWith(t1, t2);
				bool t2t1 = IsCompatibleWith(t2, t1);
				if (t1t2 && !t2t1) return 1;
				if (t2t1 && !t1t2) return -1;
				if (IsSignedIntegralType(t1) && IsUnsignedIntegralType(t2)) return 1;
				if (IsSignedIntegralType(t2) && IsUnsignedIntegralType(t1)) return -1;
				return 0;
			}

			Expression GenerateEqual(Expression left, Expression right)
			{
				return Expr.Equal(left, right);
			}

			Expression GenerateNotEqual(Expression left, Expression right)
			{
				return Expr.NotEqual(left, right);
			}

			Expression GenerateGreaterThan(Expression left, Expression right)
			{
				if (left.Type == typeof(string))
				{
					return Expr.GreaterThan(
						GenerateStaticMethodCall("Compare", left, right),
						Expr.Constant(0)
					);
				}
				return Expr.GreaterThan(left, right);
			}

			Expression GenerateGreaterThanEqual(Expression left, Expression right)
			{
				if (left.Type == typeof(string))
				{
					return Expr.GreaterThanOrEqual(
						GenerateStaticMethodCall("Compare", left, right),
						Expr.Constant(0)
					);
				}
				return Expr.GreaterThanOrEqual(left, right);
			}

			Expression GenerateLessThan(Expression left, Expression right)
			{
				if (left.Type == typeof(string))
				{
					return Expr.LessThan(
						GenerateStaticMethodCall("Compare", left, right),
						Expr.Constant(0)
					);
				}
				return Expr.LessThan(left, right);
			}

			Expression GenerateLessThanEqual(Expression left, Expression right)
			{
				if (left.Type == typeof(string))
				{
					return Expr.LessThanOrEqual(
						GenerateStaticMethodCall("Compare", left, right),
						Expr.Constant(0)
					);
				}
				return Expr.LessThanOrEqual(left, right);
			}

			Expression GenerateAdd(Expression left, Expression right)
			{
				if (left.Type == typeof(string) && right.Type == typeof(string))
				{
					return GenerateStaticMethodCall("Concat", left, right);
				}
				return Expr.Add(left, right);
			}

			Expression GenerateSubtract(Expression left, Expression right)
			{
				return Expr.Subtract(left, right);
			}

			Expression GenerateStringConcat(Expression left, Expression right)
			{
				return Expr.Call(
					null,
					typeof(string).GetMethod("Concat", new[] { typeof(object), typeof(object) }),
					new[] { PromoteExpression(left, typeof(string), true), PromoteExpression(right, typeof(string), true) });
			}

			MethodInfo GetStaticMethod(string methodName, Expression left, Expression right)
			{
				return left.Type.GetMethod(methodName, new[] { left.Type, right.Type });
			}

			Expression GenerateStaticMethodCall(string methodName, Expression left, Expression right)
			{
				return Expr.Call(null, GetStaticMethod(methodName, left, right), new[] { left, right });
			}

			void SetTextPos(int pos)
			{
				textPos = pos;
				ch = textPos < textLen ? text[textPos] : '\0';
			}

			void NextChar()
			{
				if (textPos < textLen) textPos++;
				ch = textPos < textLen ? text[textPos] : '\0';
			}

			void NextToken()
			{
				while (Char.IsWhiteSpace(ch)) NextChar();
				TokenId t;
				int tokenPos = textPos;
				switch (ch)
				{
					case '!':
						NextChar();
						if (ch == '=')
						{
							NextChar();
							t = TokenId.ExclamationEqual;
						}
						else
						{
							t = TokenId.Exclamation;
						}
						break;
					case '%':
						NextChar();
						t = TokenId.Percent;
						break;
					case '&':
						NextChar();
						if (ch == '&')
						{
							NextChar();
							t = TokenId.DoubleAmphersand;
						}
						else
						{
							t = TokenId.Amphersand;
						}
						break;
					case '(':
						NextChar();
						t = TokenId.OpenParen;
						break;
					case ')':
						NextChar();
						t = TokenId.CloseParen;
						break;
					case '*':
						NextChar();
						t = TokenId.Asterisk;
						break;
					case '+':
						NextChar();
						t = TokenId.Plus;
						break;
					case ',':
						NextChar();
						t = TokenId.Comma;
						break;
					case '-':
						NextChar();
						t = TokenId.Minus;
						break;
					case '.':
						NextChar();

						if (!Char.IsDigit(ch))
							t = TokenId.Dot;
						else
						{
							ParseDigitToken();
							t = TokenId.RealLiteral;
						}

						break;
					case '/':
						NextChar();
						t = TokenId.Slash;
						break;
					case ':':
						NextChar();
						t = TokenId.Colon;
						break;
					case '<':
						NextChar();
						if (ch == '=')
						{
							NextChar();
							t = TokenId.LessThanEqual;
						}
						else if (ch == '>')
						{
							NextChar();
							t = TokenId.LessGreater;
						}
						else
						{
							t = TokenId.LessThan;
						}
						break;
					case '=':
						NextChar();
						if (ch == '=')
						{
							NextChar();
							t = TokenId.DoubleEqual;
						}
						else
						{
							t = TokenId.Equal;
						}
						break;
					case '>':
						NextChar();
						if (ch == '=')
						{
							NextChar();
							t = TokenId.GreaterThanEqual;
						}
						else
						{
							t = TokenId.GreaterThan;
						}
						break;
					case '?':
						NextChar();
						t = TokenId.Question;
						break;
					case '[':
						NextChar();
						t = TokenId.OpenBracket;
						break;
					case ']':
						NextChar();
						t = TokenId.CloseBracket;
						break;
					case '|':
						NextChar();
						if (ch == '|')
						{
							NextChar();
							t = TokenId.DoubleBar;
						}
						else
						{
							t = TokenId.Bar;
						}
						break;
					case '"':
					case '\'':
						char quote = ch;
						do
						{
							NextChar();
							while (textPos < textLen && ch != quote) NextChar();
							if (textPos == textLen)
								throw ParseError(textPos, ParseErrorType.UnterminatedStringLiteral);
							NextChar();
						} while (ch == quote);
						t = TokenId.StringLiteral;
						break;
					default:
						if (Char.IsLetter(ch) || ch == '@' || ch == '_')
						{
							do
							{
								NextChar();
							} while (Char.IsLetterOrDigit(ch) || ch == '_');
							var identifier = text.Substring(tokenPos, textPos - tokenPos).ToLower();
							switch (identifier)
							{
								case "if": t = TokenId.If; break;
								case "then": t = TokenId.Then; break;
								case "else": t = TokenId.Else; break;
								default: t = TokenId.Identifier; break;
							}
							break;
						}
						if (Char.IsDigit(ch))
						{
							t = ParseDigitToken();
							break;
						}
						if (textPos == textLen)
						{
							t = TokenId.End;
							break;
						}
						throw ParseError(textPos, ParseErrorType.InvalidCharacter, ch);
				}
				prevToken = token;
				token.id = t;
				token.text = text.Substring(tokenPos, textPos - tokenPos);
				token.pos = tokenPos;
			}

			TokenId ParseDigitToken()
			{
				TokenId t = TokenId.IntegerLiteral;
				do
				{
					NextChar();
				} while (Char.IsDigit(ch));
				if (ch.ToString() == ".")
				{
					t = TokenId.RealLiteral;
					NextChar();
					ValidateDigit();
					do
					{
						NextChar();
					} while (Char.IsDigit(ch));
				}
				if (ch == 'E' || ch == 'e')
				{
					t = TokenId.RealLiteral;
					NextChar();
					if (ch == '+' || ch == '-') NextChar();
					ValidateDigit();
					do
					{
						NextChar();
					} while (Char.IsDigit(ch));
				}
				if (ch == 'F' || ch == 'f') NextChar();

				return t;
			}

			bool TokenIdentifierIs(string id)
			{
				return TokenIdentifierIs(token, id);
			}

			bool TokenIdentifierIs(Token token, string id)
			{
				return token.id == TokenId.Identifier && String.Equals(id, token.text, StringComparison.OrdinalIgnoreCase);
			}

			string GetIdentifier()
			{
				ValidateToken(TokenId.Identifier, ParseErrorType.IdentifierExpected);
				string id = token.text;
				if (id.Length > 1 && id[0] == '@') id = id.Substring(1);
				return id;
			}

			void ValidateDigit()
			{
				if (!Char.IsDigit(ch)) throw ParseError(textPos, ParseErrorType.DigitExpected);
			}

			void ValidateToken(TokenId t, ParseErrorType error)
			{
				if (token.id != t) throw ParseError(error);
			}

			void ValidateToken(TokenId t)
			{
				if (token.id != t) throw ParseError(ParseErrorType.SyntaxError);
			}

			Exception ParseError(ParseErrorType error, params object[] args)
			{
				return ParseError(token.pos, error, args);
			}

			Exception ParseError(int pos, ParseErrorType error, params object[] args)
			{
				return new ParseException(error, pos, args);
			}

			static Dictionary<string, object> CreateKeywords()
			{
				Dictionary<string, object> d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
				d.Add("true", trueLiteral);
				d.Add("false", falseLiteral);
				d.Add("null", nullLiteral);
				d.Add(keywordIt, keywordIt);
				d.Add(keywordIif, keywordIif);
				d.Add(keywordNew, keywordNew);
				foreach (Type type in predefinedTypes) d.Add(type.Name, type);
				return d;
			}
		}

		#endregion

		#region ExpressionHelper

		internal static class ExpressionHelper
		{
			private static readonly MethodInfo stringFormat1 = typeof(string).GetMethod("Format", new[] { typeof(string), typeof(object) });

			private static readonly MethodInfo stringFormat2 = typeof(string).GetMethod("Format", new[] { typeof(string), typeof(object), typeof(object) });

			private static readonly MethodInfo stringFormat3 = typeof(string).GetMethod("Format", new[] { typeof(string), typeof(object), typeof(object), typeof(object) });

			private static readonly MethodInfo stringFormatN = typeof(string).GetMethod("Format", new[] { typeof(string), typeof(object[]) });

			private static readonly MethodInfo stringFormatWithProvider = typeof(string).GetMethod("Format", new[] { typeof(IFormatProvider), typeof(string), typeof(object[]) });

			/// <summary>
			/// Attempt to parse a call to 'obj.ToString(format, provider)'.
			/// </summary>
			internal static bool TryParseToStringMethod(MethodCallExpression call, out Expression callThis, out string callFormat, out IFormatProvider callProvider)
			{
				if (call.Method.IsStatic)
				{
					callThis = null;
					callFormat = null;
					callProvider = null;
					return false;
				}

				if (call.Method.Name != "ToString")
				{
					callThis = null;
					callFormat = null;
					callProvider = null;
					return false;
				}

				if (call.Arguments.Count == 1)
				{
					if (call.Arguments[0].Type == typeof(string))
					{
						callThis = call.Object;
						callFormat = call.Arguments[0] is ConstantExpression ? (string)((ConstantExpression)call.Arguments[0]).Value : null;
						callProvider = null;
						return true;
					}

					if (call.Arguments[0].Type == typeof(IFormatProvider))
					{
						callThis = call.Object;
						callFormat = null;
						callProvider = call.Arguments[0] is ConstantExpression ? (IFormatProvider)((ConstantExpression)call.Arguments[0]).Value : null;
						return true;
					}
				}
				else if (call.Arguments.Count == 2)
				{
					if (call.Arguments[0].Type == typeof(string) && call.Arguments[1].Type == typeof(IFormatProvider))
					{
						callThis = call.Object;
						callFormat = call.Arguments[0] is ConstantExpression ? (string)((ConstantExpression)call.Arguments[0]).Value : null;
						callProvider = call.Arguments[1] is ConstantExpression ? (IFormatProvider)((ConstantExpression)call.Arguments[1]).Value : null;
						return true;
					}
				}

				callThis = null;
				callFormat = null;
				callProvider = null;
				return false;
			}

			/// <summary>
			/// Attempt to parse a call to 'String.Format(provider, template, args...)'.
			/// </summary>
			internal static bool TryParseStringFormatMethod(MethodCallExpression call, out IFormatProvider callProvider, out string callFormat, out Expression[] callArgs)
			{
				if (call.Method == stringFormat1 || call.Method == stringFormat2 || call.Method == stringFormat3 || call.Method == stringFormatN || call.Method == stringFormatWithProvider)
				{
					ConstantExpression providerExpr;
					ConstantExpression formatExpr;

					if (call.Method == stringFormat1 || call.Method == stringFormat2 || call.Method == stringFormat3 || call.Method == stringFormatN)
					{
						providerExpr = null;
						formatExpr = call.Arguments[0] as ConstantExpression;

						if (call.Method == stringFormat1 || call.Method == stringFormat2 || call.Method == stringFormat3 || call.Method == stringFormatN)
						{
							callArgs = new[] { call.Arguments[1] };
						}
						else if (call.Method == stringFormat1 || call.Method == stringFormat2 || call.Method == stringFormat3 || call.Method == stringFormatN)
						{
							callArgs = new[] { call.Arguments[1], call.Arguments[2] };
						}
						else if (call.Method == stringFormat1 || call.Method == stringFormat2 || call.Method == stringFormat3 || call.Method == stringFormatN)
						{
							callArgs = new[] { call.Arguments[1], call.Arguments[2], call.Arguments[3] };
						}
						else
						{
							var paramsArrayExpr = call.Arguments[1] as NewArrayExpression;
							if (paramsArrayExpr != null)
								callArgs = paramsArrayExpr.Expressions.ToArray();
							else
								callArgs = call.Arguments.Skip(1).ToArray();
						}
					}
					else
					{
						providerExpr = call.Arguments[0] as ConstantExpression;
						formatExpr = call.Arguments[1] as ConstantExpression;

						var argsArrayExpr = call.Arguments[2] as NewArrayExpression;
						if (argsArrayExpr != null)
							callArgs = argsArrayExpr.Expressions.ToArray();
						else
							callArgs = call.Arguments.Skip(2).ToArray();
					}

					if (providerExpr != null)
						callProvider = providerExpr.Value as IFormatProvider;
					else
						callProvider = null;

					if (formatExpr != null)
						callFormat = formatExpr.Value as string;
					else
						callFormat = null;

					return true;
				}

				callProvider = null;
				callFormat = null;
				callArgs = null;
				return false;
			}

			/// <summary>
			/// Try to determine the return type of the given expression.
			/// </summary>
			internal static bool TryGetReturnType(Expression expr, out Type returnType)
			{
				Expression body;
				if (TryGetLambdaBody(expr, out body))
					expr = body;

				if (expr.NodeType == ExpressionType.Convert)
				{
					returnType = expr.Type;
					return true;
				}

				var memberExpr = expr as MemberExpression;
				if (memberExpr != null)
				{
					var property = memberExpr.Member as PropertyInfo;
					if (property != null)
					{
						returnType = property.PropertyType;
						return true;
					}

					var method = memberExpr.Member as MethodInfo;
					if (method != null)
					{
						returnType = method.ReturnType;
						return true;
					}
				}

				var callExpr = expr as MethodCallExpression;
				if (callExpr != null)
				{
					returnType = callExpr.Method.ReturnType;
					return true;
				}

				returnType = null;
				return false;
			}

			/// <summary>
			/// Try to get the model type of the 
			/// </summary>
			public static bool TryGetResultModelType(Expression expr, ModelType rootType, Func<UnaryExpression, ModelProperty> getDynamicMemberAccess, out ModelType modelType, out bool isList)
			{
				Expression body;
				if (TryGetLambdaBody(expr, out body))
					expr = body;

				var castExpr = expr as ModelCastExpression;
				if (castExpr != null)
				{
					modelType = castExpr.ModelType;
					isList = castExpr.IsList;
					return true;
				}

				var memberExpr = expr as ModelMemberExpression;
				if (memberExpr != null)
				{
					var reference = memberExpr.Property as ModelReferenceProperty;
					if (reference != null)
					{
						modelType = reference.PropertyType;
						isList = reference.IsList;
						return true;
					}

					// If the property is not a reference property,
					// then the result type is not a model type.
					modelType = null;
					isList = false;
					return false;
				}

				var call = expr as MethodCallExpression;
				if (call != null)
				{
					if (call.Method.DeclaringType == typeof(Enumerable))
					{
						var source = call.Arguments[0];

						switch (call.Method.Name)
						{
							case "First":
							case "FirstOrDefault":
							case "Last":
							case "LastOrDefault":
							case "Where":
							case "OrderBy":
							case "OrderByDescending":
							case "Except":

								// TSource First<TSource>(IEnumerable<TSource> source)
								// TSource First<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate)
								// TSource Last<TSource>(IEnumerable<TSource> source)
								// TSource Last<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate)
								// IEnumerable<TSource> Where<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate)
								// IEnumerable<TSource> OrderBy<TSource, TKey>(IEnumerable<TItem> source, Func<TSource, TKey> comparer)
								// IEnumerable<TSource> OrderByDescending<TSource, TKey>(IEnumerable<TItem> source, Func<TSource, TKey> comparer)
								// IEnumerable<TSource> Except(IEnumerable<TSource> first, IEnumerable<TSource> second)

								return TryGetResultModelType(source, rootType, getDynamicMemberAccess, out modelType, out isList);

							case "Select":

								if (call.Arguments.Count == 2)
								{
									// TResult Select<TSource, TResult>(IEnumerable<TSource> source, Func<TSource, TResult> selector)

									Expression selectorBody;
									if (TryGetLambdaBody(call.Arguments[1], out selectorBody))
									{
										return TryGetResultModelType(selectorBody, rootType, getDynamicMemberAccess, out modelType, out isList);
									}
								}

								break;

							case "Any":
							case "All":
							case "Contains":
							case "Count":
							case "Average":
							case "Min":
							case "Max":
							case "Sum":

								modelType = null;
								isList = false;
								return false;
						}
					}
				}

				var unaryExpr = expr as UnaryExpression;
				if (unaryExpr != null)
				{
					if (getDynamicMemberAccess != null)
					{
						var property = getDynamicMemberAccess(unaryExpr);
						if (property != null)
						{
							isList = property.IsList;

							var reference = property as ModelReferenceProperty;
							if (reference != null)
							{
								modelType = reference.PropertyType;
								return true;
							}

							modelType = null;
							return false;
						}
					}
				}

				Type returnType;
				if (TryGetReturnType(expr, out returnType))
				{
					bool isModelType;
					Type itemType;

					ModelType.GetTypeInfo(returnType, out isModelType, out isList, out itemType);

					if (!isModelType)
					{
						modelType = null;
						isList = false;
						return false;
					}

					if (isList)
						modelType = rootType.Context.GetModelType(itemType);
					else
						modelType = rootType.Context.GetModelType(returnType);

					return true;
				}

				modelType = null;
				isList = false;
				return false;
			}

			/// <summary>
			/// If the expression is a lambda (or wrapper), then return its body expression.
			/// </summary>
			internal static bool TryGetLambdaBody(Expression expr, out Expression body)
			{
				var lambda = expr as LambdaExpression;
				if (lambda != null)
				{
					body = lambda.Body;
					return true;
				}

				var modelLambda = expr as ModelLambdaExpression;
				if (modelLambda != null)
				{
					body = modelLambda.Body;
					return true;
				}

				body = null;
				return false;
			}

			/// <summary>
			/// Try to get the model property from a property access expression.
			/// </summary>
			internal static bool TryGetModelProperty(Expression expr, out ModelProperty property)
			{
				var propExpr = expr as ModelMemberExpression;
				if (propExpr != null)
				{
					property = propExpr.Property;
					return true;
				}

				var memberExpr = expr as MemberExpression;
				if (memberExpr != null)
				{
					var propertyInfo = memberExpr.Member as PropertyInfo;
					if (propertyInfo != null)
					{
						if (memberExpr.Expression != null)
						{
							var instanceType = memberExpr.Expression.Type;
							var instanceModelType = ModelContext.Current.GetModelType(instanceType);
							if (instanceModelType != null && instanceModelType.Properties.Contains(propertyInfo.Name))
							{
								property = instanceModelType.Properties[propertyInfo.Name];
								return true;
							}
						}
					}
				}

				property = null;
				return false;
			}

			/// <summary>
			/// Get type information for the given expression.
			/// </summary>
			internal static void GetTypeInfo(Expression expr, out bool isModelType, out bool isList, out Type itemType)
			{
				Type returnType;
				if (!TryGetReturnType(expr, out returnType))
					returnType = expr.Type;

				ModelType.GetTypeInfo(returnType, out isModelType, out isList, out itemType);
			}
		}

		#endregion

		#region FormatParser

		/// <summary>
		/// Provides methods for parsing and interpreting format strings.
		/// </summary>
		internal static class FormatParser
		{
			private static readonly Regex specifierAndPrecisionParser = new Regex("^(?<specifier>[A-Za-z])(?<precision>\\d{0,2})$", RegexOptions.Compiled);

			/// <summary>
			/// An enumeration that represents the standard .NET numeric format specifiers.
			/// https://msdn.microsoft.com/en-us/library/dwhawy9k(v=vs.110).aspx
			/// </summary>
			internal enum StandardNumberFormatSpecifier
			{
				None,
				Unknown,
				Currency,
				Decimal,
				Exponential,
				FixedPoint,
				General,
				Number,
				Percent,
				Roundtrip,
				Hexadecimal
			}

			/// <summary>
			/// Determine if the given format string is a standard numeric format specifier.
			/// </summary>
			/// <remarks>
			/// Standard Numeric Format Strings: https://msdn.microsoft.com/en-us/library/dwhawy9k(v=vs.110).aspx
			/// </remarks>
			internal static bool TryGetStandardNumericFormat(Type type, string format, CultureInfo culture, out StandardNumberFormatSpecifier specifier, out int? precision, out bool isSemantic)
			{
				if (string.IsNullOrEmpty(format))
				{
					specifier = StandardNumberFormatSpecifier.None;
					precision = null;
					isSemantic = false;
					return false;
				}

				var match = specifierAndPrecisionParser.Match(format);

				if (!match.Success)
				{
					specifier = StandardNumberFormatSpecifier.None;
					precision = null;
					isSemantic = false;
					return false;
				}

				var specifierText = match.Groups["specifier"].Value.ToLower();

				switch (specifierText)
				{
					case "c":
						specifier = StandardNumberFormatSpecifier.Currency;
						break;
					case "d":
						specifier = StandardNumberFormatSpecifier.Decimal;
						break;
					case "e":
						specifier = StandardNumberFormatSpecifier.Exponential;
						break;
					case "f":
						specifier = StandardNumberFormatSpecifier.FixedPoint;
						break;
					case "g":
						specifier = StandardNumberFormatSpecifier.General;
						break;
					case "n":
						specifier = StandardNumberFormatSpecifier.Number;
						break;
					case "p":
						specifier = StandardNumberFormatSpecifier.Percent;
						break;
					case "r":
						specifier = StandardNumberFormatSpecifier.Roundtrip;
						break;
					case "x":
						specifier = StandardNumberFormatSpecifier.Hexadecimal;
						break;
					default:
						specifier = StandardNumberFormatSpecifier.Unknown;
						break;
				}

				var precisionText = match.Groups["precision"].Value;
				if (string.IsNullOrEmpty(precisionText))
					precision = GetDefaultPrecision(type, specifier, culture);
				else
				{
					int precisionNumber;
					if (int.TryParse(precisionText, out precisionNumber))
						precision = precisionNumber;
					else
						precision = null;
				}

				isSemantic = specifier == StandardNumberFormatSpecifier.Currency || specifier == StandardNumberFormatSpecifier.Percent;

				return true;
			}

			/// <summary>
			/// Get the default precision for the given type, standard numeric format specifier, and culture.
			/// </summary>
			private static int? GetDefaultPrecision(Type type, StandardNumberFormatSpecifier specifier, CultureInfo culture)
			{
				switch (specifier)
				{
					case StandardNumberFormatSpecifier.Currency:
						return culture.NumberFormat.CurrencyDecimalDigits;

					case StandardNumberFormatSpecifier.Decimal:
						return null;

					case StandardNumberFormatSpecifier.Exponential:
						return 6;

					case StandardNumberFormatSpecifier.FixedPoint:
						return culture.NumberFormat.NumberDecimalDigits;

					case StandardNumberFormatSpecifier.General:

						// https://msdn.microsoft.com/en-us/library/dwhawy9k(v=vs.110).aspx#GFormatString

						if (type == typeof(Byte) || type == typeof(SByte))
							return 3;

						if (type == typeof(Int16) || type == typeof(UInt16))
							return 5;

						if (type == typeof(Int32) || type == typeof(UInt32))
							return 10;

						if (type == typeof(Int64))
							return 19;

						if (type == typeof(UInt64))
							return 20;

						//if (type == typeof(BigInteger))
						//	return 50;

						if (type == typeof(Single))
							return 7;

						if (type == typeof(Double))
							return 15;

						if (type == typeof(Decimal))
							return 29;

						return null;

					case StandardNumberFormatSpecifier.Number:
						return culture.NumberFormat.NumberDecimalDigits;

					case StandardNumberFormatSpecifier.Percent:
						return culture.NumberFormat.PercentDecimalDigits;

					case StandardNumberFormatSpecifier.Roundtrip:
						return null;

					case StandardNumberFormatSpecifier.Hexadecimal:
						return null;
				}

				return null;
			}

			/// <summary>
			/// Determines if the given type is a true numeric type, not simply convertable to a number.
			/// </summary>
			internal static bool IsTrueNumericType(Type type)
			{
				return type == typeof(int) || type == typeof(double) || type == typeof(decimal)
					   || type == typeof(long) || type == typeof(float) || type == typeof(short)
					   || type == typeof(ushort) || type == typeof(uint) || type == typeof(ulong);
			}
		}

		#endregion

		#region ExpressionFormatter

		/// <summary>
		/// Provides options for determining the default formatting arguments
		/// to use when formatting an expression's result as a string, as well
		/// as a method to format an object using those formatting arguments.
		/// </summary>
		internal class ExpressionFormatter
		{
			private static readonly Regex singleFormatArgParser = new Regex("^{0:(?<format>.+)}$", RegexOptions.Compiled);

			private readonly ModelExpression expression;

			private readonly CultureInfo culture;

			private bool _hasAnalyzedExpression;

			private string _format;

			private IFormatProvider _provider;

			/// <summary>
			/// Creates a new formatter for the given expression and culture.
			/// </summary>
			public ExpressionFormatter(ModelExpression expression, CultureInfo culture)
			{
				this.expression = expression;
				this.culture = culture;
			}

			/// <summary>
			/// Gets the default format string to use to
			/// format the expression result as as string.
			/// </summary>
			public string Format
			{
				get
				{
					if (!_hasAnalyzedExpression)
					{
						TryGetFormat(expression.Expression, culture, out _format, out _provider);
						_hasAnalyzedExpression = true;
					}

					return _format;
				}
			}

			/// <summary>
			/// Gets the default format provider to use to
			/// format the expression result as as string.
			/// </summary>
			public IFormatProvider Provider
			{
				get
				{
					if (!_hasAnalyzedExpression)
					{
						TryGetFormat(expression.Expression, culture, out _format, out _provider);
						_hasAnalyzedExpression = true;
					}

					return _provider;
				}
			}

			/// <summary>
			/// Get format information from a model member expression, e.g. property access.
			/// </summary>
			private static void GetPropertyFormat(ModelProperty property, out string format, out IFormatProvider provider)
			{
				format = property.Format;

				var valueProperty = property as ModelValueProperty;
				if (valueProperty != null)
					provider = valueProperty.FormatProvider;
				else
					provider = null;
			}

			/// <summary>
			/// Determine format information for the left and right operands of a binary expression and choose the best format to use.
			/// </summary>
			private static bool TryGetBinaryFormat(BinaryExpression expr, CultureInfo culture, out string format, out IFormatProvider provider)
			{
				// Attempt to get the format information for the left operand.
				bool leftIsConstant;
				Type leftConvertedFrom;
				string leftFormat;
				IFormatProvider leftProvider;
				if (!TryGetFormat(expr.Left, culture, out leftIsConstant, out leftConvertedFrom, out leftFormat, out leftProvider) && !leftIsConstant)
				{
					format = null;
					provider = null;
					return false;
				}

				// Attempt to get the format information for the right operand.
				bool rightIsConstant;
				Type rightConvertedFrom;
				string rightFormat;
				IFormatProvider rightProvider;
				if (!TryGetFormat(expr.Right, culture, out rightIsConstant, out rightConvertedFrom, out rightFormat, out rightProvider) && !rightIsConstant)
				{
					format = null;
					provider = null;
					return false;
				}

				// Attempt to choose a best format...

				// A model expression compared to true or false, e.g. Property == true
				if (rightIsConstant)
				{
					format = leftFormat;
					provider = leftProvider;
					return true;
				}

				// ...also check for inverted conditional, e.g. true == Property
				if (leftIsConstant)
				{
					format = rightFormat;
					provider = rightProvider;
					return true;
				}

				// If the format providers are different, then it would be ambiguous which one to use.
				if (!Equals(leftProvider, rightProvider))
				{
					format = null;
					provider = null;
					return false;
				}

				var resultType = expr.Type;
				var resultIsNumeric = FormatParser.IsTrueNumericType(resultType);

				var leftFormatTargetType = leftConvertedFrom ?? expr.Left.Type;
				var leftFormatIsNumeric = FormatParser.IsTrueNumericType(leftFormatTargetType);

				var rightFormatTargetType = rightConvertedFrom ?? expr.Right.Type;
				var rightFormatIsNumeric = FormatParser.IsTrueNumericType(rightFormatTargetType);

				// First, check if the formats are equivalent. 

				if (leftFormat == rightFormat)
				{
					// Determine if the format is truly equivalent, depending on the type(s)
					// that it is applied to, and whether it also applies to the result.

					if (leftFormatTargetType == rightFormatTargetType)
					{
						// The target types of the formats are equivalent, so determine
						// if the format can also be applied to the result. 

						if (leftFormatTargetType == resultType)
						{
							// The target type of the formats and the result are equivalent,
							// so the format can be applied to the result.
							format = leftFormat;
							provider = leftProvider;
							return true;
						}

						// The source type of the format is different than the result type.
						format = null;
						provider = null;
						return false;
					}

					if (leftFormatIsNumeric && rightFormatIsNumeric)
					{
						// The target types of the formats are both numeric, so determine if the
						// numeric format can also be applied to the result. 

						if (resultIsNumeric)
						{
							// The target type of the formats and the result are both numeric, so the
							// format can be applied to the result. This ignores the possibility of
							// loss of precision that could impact the format's usefulness or validity.
							format = leftFormat;
							provider = leftProvider;
							return true;
						}

						// The result is not numeric, so it can't be formatted using a numeric format string.
						format = null;
						provider = null;
						return false;
					}

					// The formats were applied to different types,
					// so not sure if the format applies to the result.
					format = null;
					provider = null;
					return false;
				}

				// Otherwise, attempt to choose the best format between the two.

				if (expr.Left.Type == expr.Right.Type)
				{
					if (resultType == typeof(bool))
					{
						// Can't decide between differing boolean formats.
						format = null;
						provider = null;
						return false;
					}

					if (resultIsNumeric)
					{
						if (leftConvertedFrom != null && !leftFormatIsNumeric)
						{
							// Converted left from a non-numeric type, so it's format would not apply to a numeric result.
							format = rightFormat;
							provider = rightProvider;
							return true;
						}

						if (rightConvertedFrom != null && !rightFormatIsNumeric)
						{
							// Converted right from a non-numeric type, so it's format would not apply to a numeric result.
							format = leftFormat;
							provider = leftProvider;
							return false;
						}

						FormatParser.StandardNumberFormatSpecifier leftSpecifier;
						int? leftPrecision;
						bool leftIsSemantic;
						var leftIsStandardFormat = FormatParser.TryGetStandardNumericFormat(leftFormatTargetType, leftFormat, culture, out leftSpecifier, out leftPrecision, out leftIsSemantic);

						FormatParser.StandardNumberFormatSpecifier rightSpecifier;
						int? rightPrecision;
						bool rightIsSemantic;
						var rightIsStandardFormat = FormatParser.TryGetStandardNumericFormat(rightFormatTargetType, rightFormat, culture, out rightSpecifier, out rightPrecision, out rightIsSemantic);

						if ((leftIsStandardFormat && (rightIsStandardFormat || string.IsNullOrEmpty(rightFormat))) || (rightIsStandardFormat && string.IsNullOrEmpty(leftFormat)))
						{
							if (leftIsSemantic)
							{
								if (rightIsSemantic)
								{
									// Both formats are semantic, so only return a format if it is
									// the same type. Use the format with the least precision.
									if (leftSpecifier == rightSpecifier && leftPrecision.HasValue && rightPrecision.HasValue)
									{
										if (leftPrecision.Value < rightPrecision.Value)
										{
											format = leftFormat;
											provider = leftProvider;
											return true;
										}

										format = rightFormat;
										provider = rightProvider;
										return true;
									}

									format = null;
									provider = null;
									return false;
								}

								format = leftFormat;
								provider = leftProvider;
								return true;
							}
							else if (rightIsSemantic)
							{
								format = rightFormat;
								provider = rightProvider;
								return true;
							}
						}
					}
					else
					{
						if (leftConvertedFrom != null && rightConvertedFrom != null)
						{
							// Both values were converted, so most likely neither format would
							// apply to the result (numeric formats may apply to different types).
							format = null;
							provider = null;
							return false;
						}
					}
				}

				// Could not choose between the two formats.
				format = null;
				provider = null;
				return false;
			}

			/// <summary>
			/// Attempt to determine format information for the given expression.
			/// </summary>
			internal static bool TryGetFormat(Expression expr, CultureInfo culture, out string format, out IFormatProvider provider)
			{
				bool isConstant;
				Type convertedFrom;

				Expression lambdaBody;
				if (ExpressionHelper.TryGetLambdaBody(expr, out lambdaBody))
					return TryGetFormat(lambdaBody, culture, out isConstant, out convertedFrom, out format, out provider);

				return TryGetFormat(expr, culture, out isConstant, out convertedFrom, out format, out provider);
			}

			/// <summary>
			/// Attempt to determine format information for the given expression.
			/// </summary>
			private static bool TryGetFormat(Expression expr, CultureInfo culture, out bool isConstant, out Type convertedFrom, out string format, out IFormatProvider provider)
			{
				if (expr is ConstantExpression)
				{
					isConstant = true;
					convertedFrom = null;
					format = null;
					provider = null;
					return false;
				}

				isConstant = false;

				var unaryExpr = expr as UnaryExpression;
				if (unaryExpr != null && unaryExpr.NodeType == ExpressionType.Convert)
				{
					convertedFrom = unaryExpr.Operand.Type;

					Type operandConvertedFrom;
					if (TryGetFormat(unaryExpr.Operand, culture, out isConstant, out operandConvertedFrom, out format, out provider))
					{
						if (operandConvertedFrom != null)
							convertedFrom = operandConvertedFrom;

						return true;
					}

					return false;
				}

				convertedFrom = null;

				var binaryExpr = expr as BinaryExpression;
				if (binaryExpr != null)
					return TryGetBinaryFormat(binaryExpr, culture, out format, out provider);

				ModelProperty property;
				if (ExpressionHelper.TryGetModelProperty(expr, out property))
				{
					// Simple property expression
					GetPropertyFormat(property, out format, out provider);
					return true;
				}

				var call = expr as MethodCallExpression;
				if (call != null && call.Method.DeclaringType != null)
				{
					if (call.Method.DeclaringType == typeof(ModelInstance))
					{
						if (call.Method.IsSpecialName && call.Method.Name == "get_Item")
						{
							var indexerArgExpr = call.Arguments[0];
							if (indexerArgExpr is ConstantExpression)
							{
								var indexerArg = ((ConstantExpression)indexerArgExpr).Value;
								if (indexerArg is ModelProperty)
								{
									GetPropertyFormat((ModelProperty)indexerArg, out format, out provider);
									return true;
								}
							}
						}

						format = null;
						provider = null;
						return false;
					}

					// Detect a call to ToString(format, provider) that is consistent with the inferred format and provider for the call object.
					Expression toStringThis;
					IFormatProvider toStringProvider;
					string toStringFormat;
					if (ExpressionHelper.TryParseToStringMethod(call, out toStringThis, out toStringFormat, out toStringProvider))
					{
						if (toStringThis != null && toStringFormat != null)
						{
							bool thisIsConstant;
							Type thisConvertedFrom;
							string thisFormat;
							IFormatProvider thisProvider;

							if (TryGetFormat(toStringThis, culture, out thisIsConstant, out thisConvertedFrom, out thisFormat, out thisProvider))
							{
								// Make sure that the format and provider match.
								if (thisFormat == toStringFormat && Equals(thisProvider, toStringProvider))
								{
									isConstant = thisIsConstant;

									// The ToString() call "converted" the argument.
									convertedFrom = thisConvertedFrom ?? (toStringThis.Type != typeof(string) ? toStringThis.Type : null);

									format = thisFormat;
									provider = thisProvider;
									return true;
								}
							}
						}

						format = null;
						provider = null;
						return false;
					}

					// Detect a call to BooleanFormatter.ToString(value, format), which uses the Exo custom format provider for booleans.
					if (call.Method.DeclaringType == typeof(BooleanFormatter))
					{
						if (call.Method.Name == "ToString" && call.Arguments.Count == 2 && call.Arguments[0].Type == typeof(Boolean) && call.Arguments[1].Type == typeof(string))
						{
							var callFormat = call.Arguments[1] is ConstantExpression ? (string)((ConstantExpression)call.Arguments[1]).Value : null;

							if (!string.IsNullOrEmpty(callFormat))
							{
								bool valueIsConstant;
								Type valueConvertedFrom;
								string valueFormat;
								IFormatProvider valueProvider;

								if (TryGetFormat(call.Arguments[0], culture, out valueIsConstant, out valueConvertedFrom, out valueFormat, out valueProvider))
								{
									// Make sure that the format and provider match.
									if (valueFormat == callFormat && Equals(valueProvider, ModelType.BooleanFormatInfo.Instance))
									{
										isConstant = valueIsConstant;

										// The ToString() call "converted" the argument.
										convertedFrom = valueConvertedFrom ?? typeof(Boolean);

										format = valueFormat;
										provider = valueProvider;
										return true;
									}
								}
							}
						}

						format = null;
						provider = null;
						return false;
					}

					// Detect a call to string.Format(provider, "{0:format}", arg) that is consistent with the inferred format and provider for the arg.
					IFormatProvider stringFormatProvider;
					string stringFormatString;
					Expression[] stringFormatArgs;
					if (ExpressionHelper.TryParseStringFormatMethod(call, out stringFormatProvider, out stringFormatString, out stringFormatArgs))
					{
						// If there is a single argument, then it may be possible
						// to treat the single argument as if it wasn't being
						// formatted (for purposes of detecting the format anyway).
						if (stringFormatArgs.Length == 1)
						{
							// Is there a format string? ... (there should be)
							if (stringFormatString != null)
							{
								// Is it of the form "{0:***}"?
								var singleArgMatch = singleFormatArgParser.Match(stringFormatString);
								if (singleArgMatch.Success)
								{
									var singleArgFormat = singleArgMatch.Groups["format"].Value;

									bool argIsConstant;
									Type argConvertedFrom;
									string argFormat;
									IFormatProvider argProvider;
									if (TryGetFormat(stringFormatArgs[0], culture, out argIsConstant, out argConvertedFrom, out argFormat, out argProvider))
									{
										// Make sure that the format and provider match.
										if (argFormat == singleArgFormat && Equals(argProvider, stringFormatProvider))
										{
											isConstant = argIsConstant;

											// The string.Format() call "converted" the argument.
											convertedFrom = argConvertedFrom ?? (stringFormatArgs[0].Type != typeof(string) ? stringFormatArgs[0].Type : null);

											format = argFormat;
											provider = argProvider;
											return true;
										}
									}
								}
							}
						}

						format = null;
						provider = null;
						return false;
					}

					if (call.Method.DeclaringType == typeof(Enumerable))
					{
						var source = call.Arguments[0];

						switch (call.Method.Name)
						{
							case "Any":
							case "All":

								if (call.Arguments.Count == 1)
								{
									// Boolean Any<TSource>(IEnumerable<TSource> source)

									format = null;
									provider = null;
									return true;
								}

								if (call.Arguments.Count == 2)
								{
									// Boolean Any<TSource>(IEnumerable<TSource> source, IEnumerable<TSource, bool> predicate)
									// Boolean All<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate)

									Expression predicateBody;
									if (ExpressionHelper.TryGetLambdaBody(call.Arguments[1], out predicateBody))
									{
										bool lamdbaIsConstant;
										Type lambdaConvertedFrom;
										return TryGetFormat(predicateBody, culture, out lamdbaIsConstant, out lambdaConvertedFrom, out format, out provider);
									}
								}

								break;

							case "First":
							case "FirstOrDefault":
							case "Last":
							case "LastOrDefault":
							case "Where":
							case "OrderBy":
							case "OrderByDescending":
							case "Except":

								// TSource First<TSource>(IEnumerable<TSource> source)
								// TSource First<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate)
								// TSource Last<TSource>(IEnumerable<TSource> source)
								// TSource Last<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate)
								// IEnumerable<TSource> Where<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate)
								// IEnumerable<TSource> OrderBy<TSource, TKey>(IEnumerable<TItem> source, Func<TSource, TKey> comparer)
								// IEnumerable<TSource> OrderByDescending<TSource, TKey>(IEnumerable<TItem> source, Func<TSource, TKey> comparer)
								// IEnumerable<TSource> Except(IEnumerable<TSource> first, IEnumerable<TSource> second)

								bool sourceIsConstant;
								Type sourceConvertedFrom;
								return TryGetFormat(source, culture, out sourceIsConstant, out sourceConvertedFrom, out format, out provider);

							case "Select":

								if (call.Arguments.Count == 2)
								{
									// TResult Select<TSource, TResult>(IEnumerable<TSource> source, Func<TSource, TResult> selector)

									Expression selectorBody;
									if (ExpressionHelper.TryGetLambdaBody(call.Arguments[1], out selectorBody))
									{
										bool lamdbaIsConstant;
										Type lambdaConvertedFrom;
										return TryGetFormat(selectorBody, culture, out lamdbaIsConstant, out lambdaConvertedFrom, out format, out provider);
									}
								}

								break;

							case "Average":
							case "Min":
							case "Max":
							case "Sum":

								if (call.Arguments.Count == 2)
								{
									// TResult Average<TSource, TResult>(IEnumerable<TSource> source, Func<TSource, TResult> selector)
									// TResult Min<TSource, TResult>(IEnumerable<TSource> source, Func<TSource, TResult> selector)
									// TResult Max<TSource, TResult>(IEnumerable<TSource> source, Func<TSource, TResult> selector)
									// TResult Sum<TSource, TResult>(IEnumerable<TSource> source, Func<TSource, TResult> selector)

									Expression selectorBody;
									if (ExpressionHelper.TryGetLambdaBody(call.Arguments[1], out selectorBody))
									{
										bool lamdbaIsConstant;
										Type lambdaConvertedFrom;
										return TryGetFormat(selectorBody, culture, out lamdbaIsConstant, out lambdaConvertedFrom, out format, out provider);
									}
								}

								break;

							case "Contains":
							case "Count":

								format = null;
								provider = null;
								return false;
						}
					}
				}

				format = null;
				provider = null;
				return false;
			}

			/// <summary>
			/// Format the given model instance list as a string.
			/// </summary>
			private static string FormatInstanceList(IEnumerable<IModelInstance> instances, string format, IFormatProvider provider)
			{
				return string.Join(", ", instances.Select(i => i.Instance.ToString(format, provider)));
			}

			/// <summary>
			/// Format the given value list as a string.
			/// </summary>
			private static string FormatValueList(IEnumerable<object> values, string format, IFormatProvider provider)
			{
				return string.Join(", ", values.Select(o => FormatValue(o, format, provider)));
			}

			/// <summary>
			/// Format the given model instance as a string.
			/// </summary>
			private static string FormatInstance(ModelInstance instance)
			{
				return instance.ToString();
			}

			/// <summary>
			/// Format the given model instance as a string.
			/// </summary>
			private static string FormatInstance(ModelInstance instance, string format)
			{
				return instance.ToString(format);
			}

			/// <summary>
			/// Format the given value as a string.
			/// </summary>
			private static string FormatValue(object value, string format, IFormatProvider provider)
			{
				if (value is DateTime)
					return DateTimeFormatter.ToString((DateTime)value, format, provider);

				var formattable = value as IFormattable;
				if (formattable != null)
					return formattable.ToString(format, provider);

				var template = string.Format("{{0:{0}}}", format);

				return string.Format(provider, template, value);
			}

			/// <summary>
			/// Format an expression result as a string using format information inferred from the expression.
			/// </summary>
			public string FormatResult(object result)
			{
				return FormatResult(result, null, null);
			}

			/// <summary>
			/// Format an expression result as a string using the given format or information inferred from the expression.
			/// </summary>
			public string FormatResult(object result, string format, IFormatProvider provider)
			{
				if (result == null)
					return "";

				// Assumes the result has already been formatted if the result is a string
				if (result is string)
					return (string)result;

				bool isModelType;
				bool isList;
				Type itemType;

				Type returnType;
				if (!ExpressionHelper.TryGetReturnType(expression.Expression, out returnType))
					returnType = expression.Expression.Type;

				ModelType.GetTypeInfo(returnType, out isModelType, out isList, out itemType);

				if (string.IsNullOrEmpty(format))
				{
					// If a format is not specified, then use information inferred from the expression.
					format = Format;
					provider = Provider;
				}

				if (isList)
				{
					var list = (IEnumerable)result;

					if (isModelType)
						return FormatInstanceList(list.Cast<IModelInstance>(), format, provider);

					return FormatValueList(list.Cast<object>(), format, provider);
				}

				if (isModelType)
				{
					if (!string.IsNullOrEmpty(format))
						return FormatInstance(((IModelInstance)result).Instance, format);

					return FormatInstance(((IModelInstance)result).Instance);
				}

				return FormatValue(result, format, provider);
			}
		}

		#endregion

		#region ExpressionWriter

		/// <summary>
		/// A class that contains various helper methods for writing expressions.
		/// </summary>
		internal static class ExpressionWriter
		{
			/// <summary>
			/// Converts the given expression to call <see cref="IModelInstance.ToString()"/>.
			/// </summary>
			public static Expr CallModelInstanceToString(Expr expr)
			{
				// ModelInstanceFormatter.ToString((IModelInstance)expr, format)
				return Expr.Call(
					// ModelInstanceFormatter.ToString
					typeof(ModelInstanceFormatter).GetMethod("ToString", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(IModelInstance) }, null),
					// ((IModelInstance)expr, format)
					new Expression[]
					{
						// (IModelInstance)expr
						Expr.Convert(expr, typeof(IModelInstance))
					});
			}

			/// <summary>
			/// Converts the given expression to call <see cref="IModelInstance.ToString()"/>.
			/// </summary>
			public static Expr CallModelInstanceToString(Expr expr, String format)
			{
				// ModelInstanceFormatter.ToString((IModelInstance)expr, format)
				return Expr.Call(
					// ModelInstanceFormatter.ToString
					typeof(ModelInstanceFormatter).GetMethod("ToString", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(IModelInstance), typeof(string) }, null),
					// ((IModelInstance)expr, format)
					new Expression[]
					{
						// (IModelInstance)expr
						Expr.Convert(expr, typeof(IModelInstance)),
						// format
						Expr.Constant(format)
					});
			}

			static MethodInfo iformattableMethod = (typeof(IFormattable)).GetMethod("ToString", new[] { typeof(String), typeof(IFormatProvider) });
			static MethodInfo booleanFormatterMethod = typeof(BooleanFormatter).GetMethod("ToString", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(Boolean), typeof(string) }, null);
			static MethodInfo dateTimeFormatterMethod = typeof(DateTimeFormatter).GetMethod("ToString", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(DateTime), typeof(string), typeof(IFormatProvider) }, null);

			/// <summary>
			/// Attempt to call ToString(format, provider) for the given expression. If a "ToString" method cannot be found which
			/// accepts the format and provider parameters, then the method returns false to indicate that it was not successful.
			/// </summary>
			public static bool TryCallToString(Expr expr, String format, IFormatProvider provider, out Expr result)
			{
				if (string.IsNullOrEmpty(format))
				{
					result = null;
					return false;
				}

				if (expr.Type == typeof(DateTime))
				{
					// DateTimeFormatter.ToString(value, format, provider)
					result = Expr.Call(
						dateTimeFormatterMethod,
						expr,
						Expr.Constant(format),
						Expr.Constant(provider, typeof(IFormatProvider))
						);

					return true;
				}

				var typeDeclaredToString = expr.Type.GetMethod("ToString", new[] { typeof(String), typeof(IFormatProvider) });
				if (typeDeclaredToString != null)
				{
					// value.ToString(format, provider)
					result = Expr.Call(expr, typeDeclaredToString, Expr.Constant(format), Expr.Constant(provider, typeof(IFormatProvider)));
					return true;
				}

				if (typeof(IFormattable).IsAssignableFrom(expr.Type))
				{
					// ((IFormattable)value).ToString(format, provider)
					result = Expr.Call(
						Expr.Convert(expr, typeof(IFormattable)),
						iformattableMethod,
						new Expression[]
						{
							Expr.Constant(format),
							Expr.Constant(provider, typeof(IFormatProvider))
						});

					return true;
				}

				if (expr.Type == typeof(Boolean) && provider is ModelType.BooleanFormatInfo)
				{
					// BooleanFormatter.ToString(value, format)
					result = Expr.Call(
						booleanFormatterMethod,
						expr,
						Expr.Constant(format)
						);

					return true;
				}

				result = null;
				return false;
			}

			/// <summary>
			/// Calls ToString() for the given expression.
			/// </summary>
			public static Expr CallToString(Expr expr)
			{
				return Expr.Call(expr, typeof(object).GetMethod("ToString", Type.EmptyTypes));
			}

			/// <summary>
			/// 
			/// </summary>
			public static Expr CallLinqCast<TSource>(Expr expr)
			{
				return Expr.Call(typeof(Enumerable).GetMethod("Cast").MakeGenericMethod(typeof(TSource)), expr);
			}

			/// <summary>
			/// Call Linq 'Select' with a source type that is only known at run time, and a result type that is known at design time.
			/// </summary>
			public static Expr CallLinqSelect<TResult>(Expr expr, Type sourceType, Func<Expr, Expr> selector)
			{
				var selectMethod = typeof(Enumerable).GetMethods()
					.Single(
						m => m.Name == "Select" && m.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Func<,>));

				// IEnumerable<TResult> Select<TSource, TResult>(IEnumerable<TSource> list, (TSource i) => selector(i))
				var instanceParam = Expr.Parameter(typeof(object));
				return Expr.Call(selectMethod.MakeGenericMethod(typeof(object), typeof(TResult)),
					// IEnumerable<TSource> list
					expr,
					// (TSource i) => selector(i)
					Expr.Lambda<Func<object, TResult>>(
					// selector
						selector(Expr.Convert(instanceParam, sourceType)),
					// (TSource i)
						instanceParam)
					);
			}

			/// <summary>
			/// Call Linq 'Select' with a source and result type that are both known at design time.
			/// </summary>
			public static Expr CallLinqSelect<TSource, TResult>(Expr expr, Func<Expr, Expr> selector)
			{
				var selectMethod = typeof(Enumerable).GetMethods()
					.Single(
						m => m.Name == "Select" && m.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Func<,>));

				// IEnumerable<TResult> Select<TSource, TResult>(IEnumerable<TSource> list, (TSource i) => selector(i))
				var instanceParam = Expr.Parameter(typeof(TSource));
				return Expr.Call(selectMethod.MakeGenericMethod(typeof(TSource), typeof(TResult)),
					// IEnumerable<TSource> list
					expr,
					// (TSource i) => selector(i)
					Expr.Lambda<Func<TSource, TResult>>(
					// selector
						selector(instanceParam),
					// (TSource i)
						instanceParam)
					);
			}

			/// <summary>
			/// 
			/// </summary>
			public static Expr CallLinqToArray(Expr expr, Type sourceType)
			{
				return Expr.Call(typeof(Enumerable).GetMethod("ToArray").MakeGenericMethod(sourceType), expr);
			}

			/// <summary>
			/// 
			/// </summary>
			public static Expression CallStringJoin(string seperator, Expression values, Type arrayType)
			{
				return Expr.Call(typeof(string).GetMethod("Join", new[] { typeof(string), arrayType }), new[]
				{
					Expr.Constant(seperator),
					values
				});
			}
		}

		#endregion

		#region ExpressionCompiler

		/// <summary>
		/// Compiles an expression in preparation for direct usage against the model, which
		/// involves stripping out <see cref="ModelMemberExpression"/> expressions created by the <see cref="ExpressionParser"/>
		/// with either direct member invocations or property gets via <see cref="ModelInstance"/>.
		/// </summary>
		internal class ExpressionCompiler : ExpressionVisitor
		{
			Dictionary<ModelParameterExpression, ParameterExpression> parameterMapping;

			ExpressionCompiler()
			{ }

			public static LambdaExpression Compile(Expression expression, ModelParameterExpression root)
			{
				var parameterMapping = new Dictionary<ModelParameterExpression, ParameterExpression>();
				var resultExpression = new ExpressionCompiler() { parameterMapping = parameterMapping }.Visit(expression);

				ParameterExpression rootParam;
				if (parameterMapping.TryGetValue(root, out rootParam))
					return Expr.Lambda(resultExpression, new ParameterExpression[] { rootParam });
				else
					return Expr.Lambda(resultExpression);
			}

			protected override Expression VisitModelParameter(ModelParameterExpression m)
			{
				ParameterExpression p;
				if (!parameterMapping.TryGetValue(m, out p))
					parameterMapping[m] = p = Expr.Parameter(m.Type, m.Name);
				return p;
			}

			protected override Expression VisitModelLambda(ModelLambdaExpression m)
			{
				// Visit the parameters first to set up parameter mappings
				var parameters = m.Parameters.Select(p => (ParameterExpression)VisitModelParameter(p)).ToArray();

				return Expr.Lambda(Visit(m.Body), parameters);
			}

			protected override Expression VisitModelCastExpression(ModelCastExpression m)
			{
				return Visit(m.Expression);
			}

			protected override Expression VisitModelMember(ModelMemberExpression m)
			{
				// Attempt to coerce the property get into a simple member invocation
				if (m.Property is IReflectionModelProperty)
					return Expr.MakeMemberAccess(Visit(m.Expression), ((IReflectionModelProperty)m.Property).PropertyInfo);

				// Handle enum model type properties.
				else if (m.Property.DeclaringType is EnumTypeProvider.EnumModelType)
				{
					if (m.Property.Name == "Id")
					{
						// EnumExtensions.GetId((Enum)enum)
						return Expr.Call(
							typeof(EnumExtensions).GetMethod("GetId", new[] { typeof(Enum) }),
							Expr.Convert(Visit(m.Expression), typeof(Enum))
							);
					}

					if (m.Property.Name == "Name")
					{
						// EnumExtensions.GetName((Enum)enum)
						return Expr.Call(
							typeof(EnumExtensions).GetMethod("GetName", new[] { typeof(Enum) }),
							Expr.Convert(Visit(m.Expression), typeof(Enum))
							);
					}

					if (m.Property.Name == "DisplayName")
					{
						// EnumExtensions.GetDisplayName((Enum)enum)
						return Expr.Call(
							typeof(EnumExtensions).GetMethod("GetDisplayName", new[] { typeof(Enum) }),
							Expr.Convert(Visit(m.Expression), typeof(Enum))
							);
					}

					return m;
				}

				// Otherwise, delegate to the model instance to access the property value
				else
					return

						// (PropertyType)ModelInstance.GetModelInstance(instance)["PropertyName"]
						Expr.Convert(

							// ModelInstance.GetModelInstance(instance)["PropertyName"]
							Expr.Call(

								// ModelInstance.GetModelInstance(instance)
								Expr.Call(

									// GetModelInstance()
									typeof(ModelInstance).GetMethod("GetModelInstance"),

									// instance
									Visit(m.Expression)
								),

								// []
								typeof(ModelInstance).GetProperty("Item", new Type[] { typeof(string) }).GetGetMethod(),

								// "PropertyName"
								Expr.Constant(m.Property.Name)
							),

							// (PropertyType)
							m.Type
						);
			}
		}

		#endregion

		#region ExpressionVisitor

		/// <summary>
		/// Supports visiting <see cref="Expression"/> trees.  This class will eventually be replaced
		/// by the .NET 4.0 ExpressionVisitor framework class when 3.5 support is dropped.
		/// </summary>
		public class ExpressionVisitor
		{
			protected virtual Expression Visit(Expression exp)
			{
				if (exp == null)
					return exp;
				switch (exp.NodeType)
				{
					case ExpressionType.Negate:
					case ExpressionType.NegateChecked:
					case ExpressionType.Not:
					case ExpressionType.Convert:
					case ExpressionType.ConvertChecked:
					case ExpressionType.ArrayLength:
					case ExpressionType.Quote:
					case ExpressionType.TypeAs:
						return this.VisitUnary((UnaryExpression)exp);
					case ExpressionType.Add:
					case ExpressionType.AddChecked:
					case ExpressionType.Subtract:
					case ExpressionType.SubtractChecked:
					case ExpressionType.Multiply:
					case ExpressionType.MultiplyChecked:
					case ExpressionType.Divide:
					case ExpressionType.Modulo:
					case ExpressionType.And:
					case ExpressionType.AndAlso:
					case ExpressionType.Or:
					case ExpressionType.OrElse:
					case ExpressionType.LessThan:
					case ExpressionType.LessThanOrEqual:
					case ExpressionType.GreaterThan:
					case ExpressionType.GreaterThanOrEqual:
					case ExpressionType.Equal:
					case ExpressionType.NotEqual:
					case ExpressionType.Coalesce:
					case ExpressionType.ArrayIndex:
					case ExpressionType.RightShift:
					case ExpressionType.LeftShift:
					case ExpressionType.ExclusiveOr:
						return this.VisitBinary((BinaryExpression)exp);
					case ExpressionType.TypeIs:
						return this.VisitTypeIs((TypeBinaryExpression)exp);
					case ExpressionType.Conditional:
						return this.VisitConditional((ConditionalExpression)exp);
					case ExpressionType.Constant:
						return this.VisitConstant((ConstantExpression)exp);
					case ExpressionType.Parameter:
						if (exp is ModelExpression.ModelParameterExpression)
							return this.VisitModelParameter((ModelExpression.ModelParameterExpression)exp);
						else
							return this.VisitParameter((ParameterExpression)exp);
					case ExpressionType.MemberAccess:
						return this.VisitMemberAccess((MemberExpression)exp);
					case ExpressionType.Call:
						if (exp is ModelExpression.ModelMemberExpression)
							return this.VisitModelMember((ModelExpression.ModelMemberExpression)exp);
						else if (exp is ModelExpression.ModelCastExpression)
							return this.VisitModelCastExpression((ModelExpression.ModelCastExpression)exp);
						else
							return this.VisitMethodCall((MethodCallExpression)exp);
					case ExpressionType.Lambda:
						if (exp is ModelExpression.ModelLambdaExpression)
							return this.VisitModelLambda((ModelExpression.ModelLambdaExpression)exp);
						else
							return this.VisitLambda((LambdaExpression)exp);
					case ExpressionType.New:
						return this.VisitNew((NewExpression)exp);
					case ExpressionType.NewArrayInit:
					case ExpressionType.NewArrayBounds:
						return this.VisitNewArray((NewArrayExpression)exp);
					case ExpressionType.Invoke:
						return this.VisitInvocation((InvocationExpression)exp);
					case ExpressionType.MemberInit:
						return this.VisitMemberInit((MemberInitExpression)exp);
					case ExpressionType.ListInit:
						return this.VisitListInit((ListInitExpression)exp);
					default:
						throw new Exception(string.Format("Unhandled expression type: '{0}'", exp.NodeType));
				}
			}

			protected virtual MemberBinding VisitBinding(MemberBinding binding)
			{
				switch (binding.BindingType)
				{
					case MemberBindingType.Assignment:
						return this.VisitMemberAssignment((MemberAssignment)binding);
					case MemberBindingType.MemberBinding:
						return this.VisitMemberMemberBinding((MemberMemberBinding)binding);
					case MemberBindingType.ListBinding:
						return this.VisitMemberListBinding((MemberListBinding)binding);
					default:
						throw new Exception(string.Format("Unhandled binding type '{0}'", binding.BindingType));
				}
			}

			protected virtual ElementInit VisitElementInitializer(ElementInit initializer)
			{
				ReadOnlyCollection<Expression> arguments = this.VisitExpressionList(initializer.Arguments);
				if (arguments != initializer.Arguments)
				{
					return Expr.ElementInit(initializer.AddMethod, arguments);
				}
				return initializer;
			}

			protected virtual Expression VisitUnary(UnaryExpression u)
			{
				Expression operand = this.Visit(u.Operand);
				if (operand != u.Operand)
				{
					return Expr.MakeUnary(u.NodeType, operand, u.Type, u.Method);
				}
				return u;
			}

			protected virtual Expression VisitBinary(BinaryExpression b)
			{
				Expression left = this.Visit(b.Left);
				Expression right = this.Visit(b.Right);
				Expression conversion = this.Visit(b.Conversion);
				if (left != b.Left || right != b.Right || conversion != b.Conversion)
				{
					if (b.NodeType == ExpressionType.Coalesce && b.Conversion != null)
						return Expr.Coalesce(left, right, conversion as LambdaExpression);
					else
						return Expr.MakeBinary(b.NodeType, left, right, b.IsLiftedToNull, b.Method);
				}
				return b;
			}

			protected virtual Expression VisitTypeIs(TypeBinaryExpression b)
			{
				Expression expr = this.Visit(b.Expression);
				if (expr != b.Expression)
				{
					return Expr.TypeIs(expr, b.TypeOperand);
				}
				return b;
			}

			protected virtual Expression VisitConstant(ConstantExpression c)
			{
				return c;
			}

			protected virtual Expression VisitConditional(ConditionalExpression c)
			{
				Expression test = this.Visit(c.Test);
				Expression ifTrue = this.Visit(c.IfTrue);
				Expression ifFalse = this.Visit(c.IfFalse);
				if (test != c.Test || ifTrue != c.IfTrue || ifFalse != c.IfFalse)
				{
					return Expr.Condition(test, ifTrue, ifFalse);
				}
				return c;
			}

			protected virtual Expression VisitParameter(ParameterExpression p)
			{
				return p;
			}

			protected virtual Expression VisitModelParameter(ModelExpression.ModelParameterExpression m)
			{
				return m;
			}

			protected virtual Expression VisitModelMember(ModelExpression.ModelMemberExpression m)
			{
				Expression exp = this.Visit(m.Expression);
				if (exp != m.Expression)
					return new ModelExpression.ModelMemberExpression(exp, m.Property);
				return m;
			}

			protected virtual Expression VisitModelLambda(ModelExpression.ModelLambdaExpression m)
			{
				Expression exp = this.Visit(m.Body);
				var parameters = VisitExpressionList(m.Parameters);

				if (exp != m.Body || parameters != m.Parameters)
					return new ModelExpression.ModelLambdaExpression(exp, parameters.ToArray());
				return m;
			}

			protected virtual Expression VisitMemberAccess(MemberExpression m)
			{
				Expression exp = this.Visit(m.Expression);
				if (exp != m.Expression)
					return Expr.MakeMemberAccess(exp, m.Member);
				return m;
			}

			protected virtual Expression VisitMethodCall(MethodCallExpression m)
			{
				Expression obj = this.Visit(m.Object);
				IEnumerable<Expression> args = this.VisitExpressionList(m.Arguments);
				if (obj != m.Object || args != m.Arguments)
				{
					return Expr.Call(obj, m.Method, args);
				}
				return m;
			}

			protected virtual Expression VisitModelCastExpression(ModelExpression.ModelCastExpression m)
			{
				Expression obj = this.Visit(m.Expression.Object);
				IEnumerable<Expression> args = this.VisitExpressionList(m.Expression.Arguments);
				if (obj != m.Expression.Object || args != m.Expression.Arguments)
				{
					return new ModelExpression.ModelCastExpression(Expr.Call(obj, m.Expression.Method, args), m.ModelType, m.IsList);
				}
				return m;
			}

			protected virtual ReadOnlyCollection<T> VisitExpressionList<T>(ReadOnlyCollection<T> original)
				where T : Expression
			{
				List<T> list = null;
				for (int i = 0, n = original.Count; i < n; i++)
				{
					var p = this.Visit(original[i]) as T;
					if (list != null)
					{
						list.Add(p);
					}
					else if (p != original[i])
					{
						list = new List<T>(n);
						for (int j = 0; j < i; j++)
						{
							list.Add(original[j]);
						}
						list.Add(p);
					}
				}
				if (list != null)
				{
					return list.AsReadOnly();
				}
				return original;
			}

			protected virtual MemberAssignment VisitMemberAssignment(MemberAssignment assignment)
			{
				Expression e = this.Visit(assignment.Expression);
				if (e != assignment.Expression)
				{
					return Expr.Bind(assignment.Member, e);
				}
				return assignment;
			}

			protected virtual MemberMemberBinding VisitMemberMemberBinding(MemberMemberBinding binding)
			{
				IEnumerable<MemberBinding> bindings = this.VisitBindingList(binding.Bindings);
				if (bindings != binding.Bindings)
				{
					return Expr.MemberBind(binding.Member, bindings);
				}
				return binding;
			}

			protected virtual MemberListBinding VisitMemberListBinding(MemberListBinding binding)
			{
				IEnumerable<ElementInit> initializers = this.VisitElementInitializerList(binding.Initializers);
				if (initializers != binding.Initializers)
				{
					return Expr.ListBind(binding.Member, initializers);
				}
				return binding;
			}

			protected virtual IEnumerable<MemberBinding> VisitBindingList(ReadOnlyCollection<MemberBinding> original)
			{
				List<MemberBinding> list = null;
				for (int i = 0, n = original.Count; i < n; i++)
				{
					MemberBinding b = this.VisitBinding(original[i]);
					if (list != null)
					{
						list.Add(b);
					}
					else if (b != original[i])
					{
						list = new List<MemberBinding>(n);
						for (int j = 0; j < i; j++)
						{
							list.Add(original[j]);
						}
						list.Add(b);
					}
				}
				if (list != null)
					return list;
				return original;
			}

			protected virtual IEnumerable<ElementInit> VisitElementInitializerList(ReadOnlyCollection<ElementInit> original)
			{
				List<ElementInit> list = null;
				for (int i = 0, n = original.Count; i < n; i++)
				{
					ElementInit init = this.VisitElementInitializer(original[i]);
					if (list != null)
					{
						list.Add(init);
					}
					else if (init != original[i])
					{
						list = new List<ElementInit>(n);
						for (int j = 0; j < i; j++)
						{
							list.Add(original[j]);
						}
						list.Add(init);
					}
				}
				if (list != null)
					return list;
				return original;
			}

			protected virtual Expression VisitLambda(LambdaExpression lambda)
			{
				Expression body = this.Visit(lambda.Body);
				if (body != lambda.Body)
				{
					return Expr.Lambda(lambda.Type, body, lambda.Parameters);
				}
				return lambda;
			}

			protected virtual NewExpression VisitNew(NewExpression nex)
			{
				IEnumerable<Expression> args = this.VisitExpressionList(nex.Arguments);
				if (args != nex.Arguments)
				{
					if (nex.Members != null)
						return Expr.New(nex.Constructor, args, nex.Members);
					else
						return Expr.New(nex.Constructor, args);
				}
				return nex;
			}

			protected virtual Expression VisitMemberInit(MemberInitExpression init)
			{
				NewExpression n = this.VisitNew(init.NewExpression);
				IEnumerable<MemberBinding> bindings = this.VisitBindingList(init.Bindings);
				if (n != init.NewExpression || bindings != init.Bindings)
				{
					return Expr.MemberInit(n, bindings);
				}
				return init;
			}

			protected virtual Expression VisitListInit(ListInitExpression init)
			{
				NewExpression n = this.VisitNew(init.NewExpression);
				IEnumerable<ElementInit> initializers = this.VisitElementInitializerList(init.Initializers);
				if (n != init.NewExpression || initializers != init.Initializers)
				{
					return Expr.ListInit(n, initializers);
				}
				return init;
			}

			protected virtual Expression VisitNewArray(NewArrayExpression na)
			{
				IEnumerable<Expression> exprs = this.VisitExpressionList(na.Expressions);
				if (exprs != na.Expressions)
				{
					if (na.NodeType == ExpressionType.NewArrayInit)
					{
						return Expr.NewArrayInit(na.Type.GetElementType(), exprs);
					}
					else
					{
						return Expr.NewArrayBounds(na.Type.GetElementType(), exprs);
					}
				}
				return na;
			}

			protected virtual Expression VisitInvocation(InvocationExpression iv)
			{
				IEnumerable<Expression> args = this.VisitExpressionList(iv.Arguments);
				Expression expr = this.Visit(iv.Expression);
				if (args != iv.Arguments || expr != iv.Expression)
				{
					return Expr.Invoke(expr, args);
				}
				return iv;
			}
		}

		#endregion

		#region BooleanFormatter

		/// <summary>
		/// A class that implements Exo-specific formatting for boolean values. 
		/// </summary>
		public static class BooleanFormatter
		{
			/// <summary>
			/// Formats the given boolean value using the given format string and the Exo boolean format provider.
			/// </summary>
			internal static string ToString(Boolean value, string format)
			{
				var formatTemplate = "{0:" + format + "}";
				return string.Format(ModelType.BooleanFormatInfo.Instance, formatTemplate, value);
			}
		}

		#endregion

		#region DateTimeFormatter

		/// <summary>
		/// A class that implements Exo-specific formatting for date/time values. 
		/// </summary>
		public static class DateTimeFormatter
		{
			/// <summary>
			/// Formats the given date/time value using the given format string and the Exo date/time format provider.
			/// </summary>
			internal static string ToString(DateTime date, string format, IFormatProvider formatProvider)
			{
				if (format.ToLower() == "d" || format.ToLower() == "t")
					return date.ToString(format, formatProvider);
				else
				{
					var timezoneProvider = CultureInfo.CurrentCulture as ITimeZoneProvider;
					var timezone = timezoneProvider != null ? timezoneProvider.TimeZone : TimeZoneInfo.Local;

					return TimeZoneInfo.ConvertTime(date, timezone).ToString(format, formatProvider);
				}
			}
		}

		#endregion

		#region ModelInstanceFormatter

		/// <summary>
		/// A class that implements formatting for model instances. 
		/// </summary>
		public static class ModelInstanceFormatter
		{
			/// <summary>
			/// Formats the given model instance using the default type-level format specifier.
			/// </summary>
			internal static string ToString(IModelInstance instance)
			{
				return instance.Instance.ToString();
			}

			/// <summary>
			/// Formats the given model instance using the given format string.
			/// </summary>
			internal static string ToString(IModelInstance instance, string format)
			{
				return instance.Instance.ToString(format);
			}
		}

		#endregion

		#region QuerySyntax

		public enum QuerySyntax
		{
			DotNet,
			OData
		}

		#endregion
	}
}

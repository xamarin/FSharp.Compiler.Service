// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

/// Functions associated with writing binaries which 
/// vary between supported implementations of the CLI Common Language 
/// Runtime, e.g. between the SSCLI, Mono and the Microsoft CLR.
///
/// The implementation of the functions can be found in ilsupp-*.fs
module internal Microsoft.FSharp.Compiler.AbstractIL.Internal.Support


type PdbReader
type PdbWriter
val pdbReadClose: PdbReader -> unit
val pdbInitialize : string -> string -> PdbWriter
val absilWriteGetTimeStamp: unit -> int32


#if SILVERLIGHT
#else
open System
open System.Runtime.InteropServices
open System.Diagnostics.SymbolStore
open Internal.Utilities
open Microsoft.FSharp.Compiler.AbstractIL
open Microsoft.FSharp.Compiler.AbstractIL.Internal
open Microsoft.FSharp.Compiler.AbstractIL.IL 

type IStream = System.Runtime.InteropServices.ComTypes.IStream


/// Takes the output file name and returns debug file name.
val getDebugFileName: string -> string

/// Unmanaged resource file linker - for native resources (not managed ones).
/// The function may be called twice, once with a zero-RVA and
/// arbitrary buffer, and once with the real buffer.  The size of the
/// required buffer is returned.
type PEFileType = X86 | X64

val linkNativeResources: unlinkedResources:byte[] list ->  rva:int32 -> PEFileType -> tempFilePath:string -> byte[]
val unlinkResource: int32 -> byte[] -> byte[]

/// PDB reader and associated types
type PdbDocument
type PdbMethod
type PdbVariable
type PdbMethodScope

type PdbSequencePoint = 
    { pdbSeqPointOffset: int;
      pdbSeqPointDocument: PdbDocument;
      pdbSeqPointLine: int;
      pdbSeqPointColumn: int;
      pdbSeqPointEndLine: int;
      pdbSeqPointEndColumn: int; }

val pdbReadOpen: string (* module *) -> string (* path *) -> PdbReader
val pdbReadClose: PdbReader -> unit
val pdbReaderGetMethod: PdbReader -> int32 (* token *) -> PdbMethod
val pdbReaderGetMethodFromDocumentPosition: PdbReader -> PdbDocument -> int (* line *) -> int (* col *) -> PdbMethod
val pdbReaderGetDocuments: PdbReader -> PdbDocument array
val pdbReaderGetDocument: PdbReader -> string (* url *) -> byte[] (* guid *) -> byte[] (* guid *) -> byte[] (* guid *) -> PdbDocument

val pdbDocumentGetURL: PdbDocument -> string
val pdbDocumentGetType: PdbDocument -> byte[] (* guid *)
val pdbDocumentGetLanguage: PdbDocument -> byte[] (* guid *)
val pdbDocumentGetLanguageVendor: PdbDocument -> byte[] (* guid *)
val pdbDocumentFindClosestLine: PdbDocument -> int -> int

val pdbMethodGetToken: PdbMethod -> int32
val pdbMethodGetRootScope: PdbMethod ->  PdbMethodScope
val pdbMethodGetSequencePoints: PdbMethod -> PdbSequencePoint array

val pdbScopeGetChildren: PdbMethodScope -> PdbMethodScope array
val pdbScopeGetOffsets: PdbMethodScope -> int * int
val pdbScopeGetLocals: PdbMethodScope -> PdbVariable array

val pdbVariableGetName: PdbVariable -> string
val pdbVariableGetSignature: PdbVariable -> byte[]
val pdbVariableGetAddressAttributes: PdbVariable -> int32 (* kind *) * int32 (* addrField1 *)


//---------------------------------------------------------------------
// PDB writer.
//---------------------------------------------------------------------

type PdbDocumentWriter

type idd =
    { iddCharacteristics: int32;
      iddMajorVersion: int32; (* actually u16 in IMAGE_DEBUG_DIRECTORY *)
      iddMinorVersion: int32; (* acutally u16 in IMAGE_DEBUG_DIRECTORY *)
      iddType: int32;
      iddData: byte[];}

val pdbInitialize: 
    string (* .exe/.dll already written and closed *) -> 
    string  (* .pdb to write *) ->
    PdbWriter
val pdbClose: PdbWriter -> unit
val pdbCloseDocument : PdbDocumentWriter -> unit
val pdbSetUserEntryPoint: PdbWriter -> int32 -> unit
val pdbDefineDocument: PdbWriter -> string -> PdbDocumentWriter
val pdbOpenMethod: PdbWriter -> int32 -> unit
val pdbCloseMethod: PdbWriter -> unit
val pdbOpenScope: PdbWriter -> int -> unit
val pdbCloseScope: PdbWriter -> int -> unit
val pdbDefineLocalVariable: PdbWriter -> string -> byte[] -> int32 -> unit
val pdbSetMethodRange: PdbWriter -> PdbDocumentWriter -> int -> int -> PdbDocumentWriter -> int -> int -> unit
val pdbDefineSequencePoints: PdbWriter -> PdbDocumentWriter -> (int * int * int * int * int) array -> unit
val pdbGetDebugInfo: PdbWriter -> idd

//---------------------------------------------------------------------
// Strong name signing
//---------------------------------------------------------------------

type keyContainerName = string
type keyPair = byte[]
type pubkey = byte[]

val signerOpenPublicKeyFile: string -> pubkey 
val signerOpenKeyPairFile: string -> keyPair 
val signerGetPublicKeyForKeyPair: keyPair -> pubkey 
val signerGetPublicKeyForKeyContainer: string -> pubkey 
val signerCloseKeyContainer: keyContainerName -> unit 
val signerSignatureSize: pubkey -> int 
val signerSignFileWithKeyPair: string -> keyPair -> unit 
val signerSignFileWithKeyContainer: string -> keyContainerName -> unit 
#endif





//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
// 
//     Tool     : bondc, Version=3.0.1, Build=bond-git.retail.not
//     Template : Microsoft.Bond.Rules.dll#Java.tt
//     File     : org\stromberg\durandal\api\LUResponse.java
//
//     Changes to this file may cause incorrect behavior and will be lost when
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
package org.stromberg.durandal.api;


/**
* LUResponse
*/
@SuppressWarnings("all")
public class LUResponse implements com.microsoft.bond.BondSerializable, com.microsoft.bond.BondMirror {
    // TODO: implement
    public com.microsoft.bond.BondSerializable clone() {return null;}

    //
    // Fields
    //

    // 1: Required int32 ProtocolVersion
    private int ProtocolVersion;

    // 2: Required vector<org.stromberg.durandal.api.RecognizedPhrase> Results
    private java.util.ArrayList<org.stromberg.durandal.api.RecognizedPhrase> Results;

    /**
     * @return current value of ProtocolVersion property
     */
    public final int getProtocolVersion() {
        return this.ProtocolVersion;
    }

    /**
     * @param value new value of ProtocolVersion property
     */
    public final void setProtocolVersion(int value) {
        this.ProtocolVersion = value;
    }

    /**
     * @return current value of Results property
     */
    public final java.util.ArrayList<org.stromberg.durandal.api.RecognizedPhrase> getResults() {
        return this.Results;
    }

    /**
     * @param value new value of Results property
     */
    public final void setResults(java.util.ArrayList<org.stromberg.durandal.api.RecognizedPhrase> value) {
        this.Results = value;
    }

    /**
     * Schema metadata
     */
    public static class Schema {
        public static final com.microsoft.bond.SchemaDef schemaDef;
        public static final com.microsoft.bond.Metadata metadata;
        private static final com.microsoft.bond.Metadata ProtocolVersion_metadata;
        private static final com.microsoft.bond.Metadata Results_metadata;

        static {
            metadata = new com.microsoft.bond.Metadata();
            metadata.setName("LUResponse");
            metadata.setQualified_name("org.stromberg.durandal.api.LUResponse");


            // ProtocolVersion
            ProtocolVersion_metadata = new com.microsoft.bond.Metadata();
            ProtocolVersion_metadata.setName("ProtocolVersion");
            ProtocolVersion_metadata.setModifier(com.microsoft.bond.Modifier.Required);
            ProtocolVersion_metadata.getDefault_value().setInt_value(6);

            // Results
            Results_metadata = new com.microsoft.bond.Metadata();
            Results_metadata.setName("Results");
            Results_metadata.setModifier(com.microsoft.bond.Modifier.Required);

            schemaDef = new com.microsoft.bond.SchemaDef();
            schemaDef.setRoot(getTypeDef(schemaDef));
        }

        public static com.microsoft.bond.TypeDef getTypeDef(com.microsoft.bond.SchemaDef schema)
        {
            com.microsoft.bond.TypeDef type = new com.microsoft.bond.TypeDef();
            type.setId(com.microsoft.bond.BondDataType.BT_STRUCT);
            type.setStruct_def(getStructDef(schema));
            return type;
        }

        private static short getStructDef(com.microsoft.bond.SchemaDef schema)
        {
            short pos;

            for(pos = 0; pos < schema.getStructs().size(); pos++)
            {
                if (schema.getStructs().get(pos).getMetadata() == metadata)
                {
                    return pos;
                }
            }

            com.microsoft.bond.StructDef structDef = new com.microsoft.bond.StructDef();
            schema.getStructs().add(structDef);

            structDef.setMetadata(metadata);

            com.microsoft.bond.FieldDef field;

            field = new com.microsoft.bond.FieldDef();
            field.setId((short)1);
            field.setMetadata(ProtocolVersion_metadata);
            field.getType().setId(com.microsoft.bond.BondDataType.BT_INT32);
            structDef.getFields().add(field);

            field = new com.microsoft.bond.FieldDef();
            field.setId((short)2);
            field.setMetadata(Results_metadata);
            field.getType().setId(com.microsoft.bond.BondDataType.BT_LIST);
            field.getType().setElement(new com.microsoft.bond.TypeDef());
            field.getType().setElement(org.stromberg.durandal.api.RecognizedPhrase.Schema.getTypeDef(schema));
            structDef.getFields().add(field);

            return pos;
        }
    }

    /*
    * @see com.microsoft.bond.BondMirror#getField()
    */
    public Object getField(com.microsoft.bond.FieldDef fieldDef) {
        switch (fieldDef.getId()) {
            case (short)1:
                return this.ProtocolVersion;
            case (short)2:
                return this.Results;
            default:
                return null;
        }
    }


    /*
    * @see com.microsoft.bond.BondMirror#setField()
    */
    public void setField(com.microsoft.bond.FieldDef fieldDef, Object value) {
        switch (fieldDef.getId()) {
            case (short)1:
                this.ProtocolVersion = (Integer)value;
                break;
            case (short)2:
                this.Results = (java.util.ArrayList<org.stromberg.durandal.api.RecognizedPhrase>)value;
                break;
        }
    }


    /*
    * @see com.microsoft.bond.BondMirror#createInstance()
    */
    public com.microsoft.bond.BondMirror createInstance(com.microsoft.bond.StructDef structDef) {
        if (org.stromberg.durandal.api.RecognizedPhrase.Schema.metadata == structDef.getMetadata()) {
            return new org.stromberg.durandal.api.RecognizedPhrase();
        }

        return null;
    }

    /*
     * @see com.microsoft.bond.BondMirror#getSchema()
     */
    public com.microsoft.bond.SchemaDef getSchema()
    {
        return getRuntimeSchema();
    }

    /**
     * Static method returning {@link SchemaDef} instance.
     */
    public static com.microsoft.bond.SchemaDef getRuntimeSchema()
    {
        return Schema.schemaDef;
    }

    // Constructor
    public LUResponse() {
        reset();
    }

    /*
     * @see com.microsoft.bond.BondSerializable#reset()
     */
    public void reset() {
        reset("LUResponse", "org.stromberg.durandal.api.LUResponse");
    }

    protected void reset(String name, String qualifiedName) {
        
        ProtocolVersion = 6;
        if (Results == null) {
            Results = new java.util.ArrayList<org.stromberg.durandal.api.RecognizedPhrase>();
        } else {
            Results.clear();
        }
    }

    /*
     * @see com.microsoft.bond.BondSerializable#unmarshal()
     */
    public void unmarshal(java.io.InputStream input) throws java.io.IOException {
        com.microsoft.bond.internal.Marshaler.unmarshal(input, this);
    }

    /*
     * @see com.microsoft.bond.BondSerializable#unmarshal()
     */
    public void unmarshal(java.io.InputStream input, com.microsoft.bond.BondSerializable schema) throws java.io.IOException {
        com.microsoft.bond.internal.Marshaler.unmarshal(input, (com.microsoft.bond.SchemaDef)schema, this);
    }

    /*
     * @see com.microsoft.bond.BondSerializable#read()
     */
    public void read(com.microsoft.bond.ProtocolReader reader) throws java.io.IOException {
        reader.readBegin();
        readImpl(reader);
        reader.readEnd();
    }

    /*
     * Called to read a struct that is contained inside another struct.
     */
    public void readImpl(com.microsoft.bond.ProtocolReader reader) throws java.io.IOException {
        if (!reader.hasCapability(com.microsoft.bond.ProtocolCapability.TAGGED)) {
            readUntagged(reader, false);
        } else if (readTagged(reader, false)) {
            com.microsoft.bond.internal.ReadHelper.skipPartialStruct(reader);
        }
    }

    /*
     * @see com.microsoft.bond.BondSerializable#read()
     */
    public void read(com.microsoft.bond.ProtocolReader reader, com.microsoft.bond.BondSerializable schema) throws java.io.IOException {
        // read(com.microsoft.bond.internal.ProtocolHelper.createReader(reader, schema));
    }

    protected void readUntagged(com.microsoft.bond.ProtocolReader reader, boolean isBase) throws java.io.IOException {
        boolean canOmitFields = reader.hasCapability(com.microsoft.bond.ProtocolCapability.CAN_OMIT_FIELDS);

        reader.readStructBegin(isBase);
        

        if (!canOmitFields || !reader.readFieldOmitted()) {
            this.ProtocolVersion = reader.readInt32();
        }
        else
        {
            // throw new BondException("Missing required field \"ProtocolVersion\", id=1");
        }

        if (!canOmitFields || !reader.readFieldOmitted()) {
            this.readFieldImpl_Results(reader, com.microsoft.bond.BondDataType.BT_LIST);
        }
        else
        {
            // throw new BondException("Missing required field \"Results\", id=2");
        }
        reader.readStructEnd();
    } // ReadUntagged()


    protected boolean readTagged(com.microsoft.bond.ProtocolReader reader, boolean isBase) throws java.io.IOException {
        boolean isPartial;
        reader.readStructBegin(isBase);

        // BitArray seenRequiredFields = new BitArray(3);

        while (true) {
            com.microsoft.bond.ProtocolReader.FieldTag fieldTag = reader.readFieldBegin();

            if (fieldTag.type == com.microsoft.bond.BondDataType.BT_STOP
             || fieldTag.type == com.microsoft.bond.BondDataType.BT_STOP_BASE) {
                isPartial = (fieldTag.type == com.microsoft.bond.BondDataType.BT_STOP_BASE);
                break;
            }

            switch (fieldTag.id) {
                case 1:
                    this.ProtocolVersion = com.microsoft.bond.internal.ReadHelper.readInt32(reader, fieldTag.type);
                    // seenRequiredFields.Set(1, true);
                    break;
                case 2:
                    this.readFieldImpl_Results(reader, fieldTag.type);
                    // seenRequiredFields.Set(2, true);
                    break;
                default:
                    reader.skip(fieldTag.type);
                    break;
            }

            reader.readFieldEnd();
        }

        reader.readStructEnd();

        //checkRequiredFieldsAreSeen(seenRequiredFields);
        return isPartial;
    }


    private void readFieldImpl_Results(com.microsoft.bond.ProtocolReader reader, com.microsoft.bond.BondDataType typeInPayload) throws java.io.IOException {
        com.microsoft.bond.internal.ReadHelper.validateType(typeInPayload, com.microsoft.bond.BondDataType.BT_LIST);
        com.microsoft.bond.ProtocolReader.ListTag tag1 = reader.readContainerBegin();
        com.microsoft.bond.internal.ReadHelper.validateType(tag1.type, com.microsoft.bond.BondDataType.BT_STRUCT);
        this.Results.ensureCapacity(tag1.size);
    
        for (int i3 = 0; i3 < tag1.size; i3++) {
            org.stromberg.durandal.api.RecognizedPhrase element2 = new org.stromberg.durandal.api.RecognizedPhrase();
                element2.readImpl(reader);
            this.Results.add(element2);
        }
    
        reader.readContainerEnd();
    } // readFieldImpl_Results


    /*
     * @see com.microsoft.bond.BondSerializable#marshal()
     */
    public void marshal(com.microsoft.bond.ProtocolWriter writer) throws java.io.IOException {
        com.microsoft.bond.internal.Marshaler.marshal(this, writer);
    }

    /*
     * @see com.microsoft.bond.BondSerializable#write()
     */
    public void write(com.microsoft.bond.ProtocolWriter writer) throws java.io.IOException {
        writer.writeBegin();
        com.microsoft.bond.ProtocolWriter firstPassWriter;
        if ((firstPassWriter = writer.getFirstPassWriter()) != null)
        {
            writeImpl(firstPassWriter, false);
            writeImpl(writer, false);
        }
        else
        {
          writeImpl(writer, false);
        }
        writer.writeEnd();
    }

    public void writeImpl(com.microsoft.bond.ProtocolWriter writer, boolean isBase) throws java.io.IOException {
        boolean canOmitFields = writer.hasCapability(com.microsoft.bond.ProtocolCapability.CAN_OMIT_FIELDS);
        writer.writeStructBegin(Schema.metadata, isBase);
        

        writer.writeFieldBegin(com.microsoft.bond.BondDataType.BT_INT32, 1, Schema.ProtocolVersion_metadata);
        writer.writeInt32(ProtocolVersion);
        writer.writeFieldEnd();

        writer.writeFieldBegin(com.microsoft.bond.BondDataType.BT_LIST, 2, Schema.Results_metadata);
        int size2 = Results.size();
            writer.writeContainerBegin(size2, com.microsoft.bond.BondDataType.BT_STRUCT);
            for (org.stromberg.durandal.api.RecognizedPhrase item1 : Results) {
                item1.writeImpl(writer, false);
            }
            writer.writeContainerEnd();
        writer.writeFieldEnd();

        writer.writeStructEnd(isBase);
    } // writeImpl


    public boolean memberwiseCompare(Object obj) {
        if (obj == null) {
            return false;
        }

        LUResponse that = (LUResponse)obj;

        return memberwiseCompareQuick(that) && memberwiseCompareDeep(that);
    }

    protected boolean memberwiseCompareQuick(LUResponse that) {
        boolean equals = true;
        
        equals = equals && (this.ProtocolVersion == that.ProtocolVersion);
        equals = equals && ((this.Results == null) == (that.Results == null));
        equals = equals && ((this.Results == null) ? true : (this.Results.size() == that.Results.size()));
        return equals;
    } // memberwiseCompareQuick

    protected boolean memberwiseCompareDeep(LUResponse that) {
        boolean equals = true;
        
        if (equals && this.Results != null && this.Results.size() != 0) {
            for (int i1 = 0; i1 < this.Results.size(); ++i1) {
                org.stromberg.durandal.api.RecognizedPhrase val2 = this.Results.get(i1);
                org.stromberg.durandal.api.RecognizedPhrase val3 = that.Results.get(i1);
                equals = equals && ((val2 == null) == (val3 == null));
                equals = equals && (val2 == null ? true : val2.memberwiseCompare(val3));
                if (!equals) {
                    break;
                }
            }
        }
        return equals;
    } // memberwiseCompareDeep

}; // class LUResponse

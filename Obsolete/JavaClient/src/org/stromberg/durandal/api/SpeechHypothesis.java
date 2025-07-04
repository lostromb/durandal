



//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
// 
//     Tool     : bondc, Version=3.0.1, Build=bond-git.retail.not
//     Template : Microsoft.Bond.Rules.dll#Java.tt
//     File     : org\stromberg\durandal\api\SpeechHypothesis.java
//
//     Changes to this file may cause incorrect behavior and will be lost when
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
package org.stromberg.durandal.api;


/**
* SpeechHypothesis
*/
@SuppressWarnings("all")
public class SpeechHypothesis implements com.microsoft.bond.BondSerializable, com.microsoft.bond.BondMirror {
    // TODO: implement
    public com.microsoft.bond.BondSerializable clone() {return null;}

    //
    // Fields
    //

    // 1: Required string Utterance
    private String Utterance;

    // 2: Required float Confidence
    private float Confidence;

    // 3: Optional string LexicalForm
    private String LexicalForm;

    /**
     * @return current value of Utterance property
     */
    public final String getUtterance() {
        return this.Utterance;
    }

    /**
     * @param value new value of Utterance property
     */
    public final void setUtterance(String value) {
        this.Utterance = value;
    }

    /**
     * @return current value of Confidence property
     */
    public final float getConfidence() {
        return this.Confidence;
    }

    /**
     * @param value new value of Confidence property
     */
    public final void setConfidence(float value) {
        this.Confidence = value;
    }

    /**
     * @return current value of LexicalForm property
     */
    public final String getLexicalForm() {
        return this.LexicalForm;
    }

    /**
     * @param value new value of LexicalForm property
     */
    public final void setLexicalForm(String value) {
        this.LexicalForm = value;
    }

    /**
     * Schema metadata
     */
    public static class Schema {
        public static final com.microsoft.bond.SchemaDef schemaDef;
        public static final com.microsoft.bond.Metadata metadata;
        private static final com.microsoft.bond.Metadata Utterance_metadata;
        private static final com.microsoft.bond.Metadata Confidence_metadata;
        private static final com.microsoft.bond.Metadata LexicalForm_metadata;

        static {
            metadata = new com.microsoft.bond.Metadata();
            metadata.setName("SpeechHypothesis");
            metadata.setQualified_name("org.stromberg.durandal.api.SpeechHypothesis");


            // Utterance
            Utterance_metadata = new com.microsoft.bond.Metadata();
            Utterance_metadata.setName("Utterance");
            Utterance_metadata.setModifier(com.microsoft.bond.Modifier.Required);
            Utterance_metadata.getDefault_value().setString_value("");

            // Confidence
            Confidence_metadata = new com.microsoft.bond.Metadata();
            Confidence_metadata.setName("Confidence");
            Confidence_metadata.setModifier(com.microsoft.bond.Modifier.Required);
            Confidence_metadata.getDefault_value().setDouble_value(0);

            // LexicalForm
            LexicalForm_metadata = new com.microsoft.bond.Metadata();
            LexicalForm_metadata.setName("LexicalForm");
            LexicalForm_metadata.getDefault_value().setString_value("");

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
            field.setMetadata(Utterance_metadata);
            field.getType().setId(com.microsoft.bond.BondDataType.BT_STRING);
            structDef.getFields().add(field);

            field = new com.microsoft.bond.FieldDef();
            field.setId((short)2);
            field.setMetadata(Confidence_metadata);
            field.getType().setId(com.microsoft.bond.BondDataType.BT_FLOAT);
            structDef.getFields().add(field);

            field = new com.microsoft.bond.FieldDef();
            field.setId((short)3);
            field.setMetadata(LexicalForm_metadata);
            field.getType().setId(com.microsoft.bond.BondDataType.BT_STRING);
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
                return this.Utterance;
            case (short)2:
                return this.Confidence;
            case (short)3:
                return this.LexicalForm;
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
                this.Utterance = (String)value;
                break;
            case (short)2:
                this.Confidence = (Float)value;
                break;
            case (short)3:
                this.LexicalForm = (String)value;
                break;
        }
    }


    /*
    * @see com.microsoft.bond.BondMirror#createInstance()
    */
    public com.microsoft.bond.BondMirror createInstance(com.microsoft.bond.StructDef structDef) {
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
    public SpeechHypothesis() {
        reset();
    }

    /*
     * @see com.microsoft.bond.BondSerializable#reset()
     */
    public void reset() {
        reset("SpeechHypothesis", "org.stromberg.durandal.api.SpeechHypothesis");
    }

    protected void reset(String name, String qualifiedName) {
        
        Utterance = "";
        Confidence = 0f;
        LexicalForm = "";
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
            this.Utterance = reader.readString();
        }
        else
        {
            // throw new BondException("Missing required field \"Utterance\", id=1");
        }

        if (!canOmitFields || !reader.readFieldOmitted()) {
            this.Confidence = reader.readFloat();
        }
        else
        {
            // throw new BondException("Missing required field \"Confidence\", id=2");
        }

        if (!canOmitFields || !reader.readFieldOmitted()) {
            this.LexicalForm = reader.readString();
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
                    this.Utterance = com.microsoft.bond.internal.ReadHelper.readString(reader, fieldTag.type);
                    // seenRequiredFields.Set(1, true);
                    break;
                case 2:
                    this.Confidence = com.microsoft.bond.internal.ReadHelper.readFloat(reader, fieldTag.type);
                    // seenRequiredFields.Set(2, true);
                    break;
                case 3:
                    this.LexicalForm = com.microsoft.bond.internal.ReadHelper.readString(reader, fieldTag.type);
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
        

        writer.writeFieldBegin(com.microsoft.bond.BondDataType.BT_STRING, 1, Schema.Utterance_metadata);
        writer.writeString(Utterance);
        writer.writeFieldEnd();

        writer.writeFieldBegin(com.microsoft.bond.BondDataType.BT_FLOAT, 2, Schema.Confidence_metadata);
        writer.writeFloat(Confidence);
        writer.writeFieldEnd();

        if (!canOmitFields || (LexicalForm != Schema.LexicalForm_metadata.getDefault_value().getString_value())) {
            writer.writeFieldBegin(com.microsoft.bond.BondDataType.BT_STRING, 3, Schema.LexicalForm_metadata);
            writer.writeString(LexicalForm);
            writer.writeFieldEnd();
        } else {
            writer.writeFieldOmitted(com.microsoft.bond.BondDataType.BT_STRING, 3, Schema.LexicalForm_metadata);
        }

        writer.writeStructEnd(isBase);
    } // writeImpl


    public boolean memberwiseCompare(Object obj) {
        if (obj == null) {
            return false;
        }

        SpeechHypothesis that = (SpeechHypothesis)obj;

        return memberwiseCompareQuick(that) && memberwiseCompareDeep(that);
    }

    protected boolean memberwiseCompareQuick(SpeechHypothesis that) {
        boolean equals = true;
        
        equals = equals && ((this.Utterance == null) == (that.Utterance == null));
        equals = equals && (this.Utterance == null ? true : (this.Utterance.length() == that.Utterance.length()));
        equals = equals && (Float.isNaN(this.Confidence) ? Float.isNaN(that.Confidence) : (this.Confidence == that.Confidence));
        equals = equals && ((this.LexicalForm == null) == (that.LexicalForm == null));
        equals = equals && (this.LexicalForm == null ? true : (this.LexicalForm.length() == that.LexicalForm.length()));
        return equals;
    } // memberwiseCompareQuick

    protected boolean memberwiseCompareDeep(SpeechHypothesis that) {
        boolean equals = true;
        
        equals = equals && (this.Utterance == null ? true : this.Utterance.equals(that.Utterance));
        equals = equals && (this.LexicalForm == null ? true : this.LexicalForm.equals(that.LexicalForm));
        return equals;
    } // memberwiseCompareDeep

}; // class SpeechHypothesis
